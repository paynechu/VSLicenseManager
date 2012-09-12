﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using EnvDTE;
using LicenseHeaderManager.Options;
using Microsoft.VisualStudio.Shell;
using Language = LicenseHeaderManager.Options.Language;

namespace LicenseHeaderManager.Headers
{
  public class LicenseHeaderReplacer
  {
    /// <summary>
    /// Used to keep track of the user selection when he is trying to insert invalid headers into all files,
    /// so that the warning is only displayed once per file extension.
    /// </summary>
    private readonly IDictionary<string, bool> _extensionsWithInvalidHeaders = new Dictionary<string, bool> ();

    private ILicenseHeaderExtension _licenseHeaderExtension;
    public LicenseHeaderReplacer(ILicenseHeaderExtension licenseHeaderExtension)
    {
      _licenseHeaderExtension = licenseHeaderExtension;
    }

    public void ResetExtensionsWithInvalidHeaders()
    {
      _extensionsWithInvalidHeaders.Clear();
    }

    /// <summary>
    /// Removes or replaces the header of a given project item.
    /// </summary>
    /// <param name="item">The project item.</param>
    /// <param name="headers">A dictionary of headers using the file extension as key and the header as value or null if headers should only be removed.</param>
    /// <param name="calledbyUser">Specifies whether the command was called by the user (as opposed to automatically by a linked command or by ItemAdded)</param>
    public void RemoveOrReplaceHeader (ProjectItem item, IDictionary<string, string[]> headers, bool calledbyUser = true)
    {
      try
      {
        Document document;
        CreateDocumentResult result = TryCreateDocument (item, out document, headers);
        string message;

        switch (result)
        {
          case CreateDocumentResult.DocumentCreated:
            if (!document.ValidateHeader ())
            {
              message = string.Format (Resources.Warning_InvalidLicenseHeader, Path.GetExtension (item.Name)).Replace (@"\n", "\n");
              if (MessageBox.Show (message, Resources.Warning, MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No)
                  == MessageBoxResult.No)
                break;
            }
            try
            {
              document.ReplaceHeaderIfNecessary ();
            }
            catch (ParseException)
            {
              message = string.Format (Resources.Error_InvalidLicenseHeader, item.Name).Replace (@"\n", "\n");
              MessageBox.Show (message, Resources.Error, MessageBoxButton.OK, MessageBoxImage.Error);
            }
            break;
          case CreateDocumentResult.LanguageNotFound:
            message = string.Format (Resources.Error_LanguageNotFound, Path.GetExtension (item.Name)).Replace (@"\n", "\n");
            if (calledbyUser && MessageBox.Show (message, Resources.Error, MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No)
                == MessageBoxResult.Yes)
              _licenseHeaderExtension.ShowLanguagesPage ();
            break;
          case CreateDocumentResult.NoHeaderFound:
            if (calledbyUser)
            {
              var page = _licenseHeaderExtension.GetDefaultLicenseHeaderPage ();
              LicenseHeader.ShowQuestionForAddingLicenseHeaderFile (item.ContainingProject, page);
            }
            break;
        }
      }
      catch (ArgumentException ex)
      {
        MessageBox.Show (ex.Message, Resources.Error, MessageBoxButton.OK, MessageBoxImage.Warning);
      }
    }

    /// <summary>
    /// Removes or replaces the header of a given project item and all of its child items.
    /// </summary>
    /// <param name="item">The project item.</param>
    /// <param name="headers">A dictionary of headers using the file extension as key and the header as value or null if headers should only be removed.</param>
    /// <param name="searchForLicenseHeaders"></param>
    public int RemoveOrReplaceHeaderRecursive (ProjectItem item, IDictionary<string, string[]> headers, bool searchForLicenseHeaders = true)
    {
      int headersFound = 0;
      bool isOpen = item.IsOpen[Constants.vsViewKindAny];

      Document document;
      if (TryCreateDocument (item, out document, headers) == CreateDocumentResult.DocumentCreated)
      {
        // item.Saved is not implemented for web_folders, therefore this check must be after the TryCreateDocument
        bool isSaved = item.Saved;

        string message;
        bool replace = true;

        if (!document.ValidateHeader ())
        {
          string extension = Path.GetExtension (item.Name);
          if (!_extensionsWithInvalidHeaders.TryGetValue (extension, out replace))
          {
            message = string.Format (Resources.Warning_InvalidLicenseHeader, extension).Replace (@"\n", "\n");
            replace = MessageBox.Show (message, Resources.Warning, MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No)
                      == MessageBoxResult.Yes;
            _extensionsWithInvalidHeaders[extension] = replace;
          }
        }

        if (replace)
        {
          try
          {
            document.ReplaceHeaderIfNecessary ();
          }
          catch (ParseException)
          {
            message = string.Format (Resources.Error_InvalidLicenseHeader, item.Name).Replace (@"\n", "\n");
            MessageBox.Show (message, Resources.Error, MessageBoxButton.OK, MessageBoxImage.Error);
          }
        }

        if (isOpen)
        {
          if (isSaved)
            item.Document.Save ();
        }
        else
          item.Document.Close (vsSaveChanges.vsSaveChangesYes);
      }
      
      if (item.ProjectItems != null)
      {
        var childHeaders = headers;
        if (searchForLicenseHeaders)
        {
          childHeaders = LicenseHeaderFinder.GetHeader (item.ProjectItems);
          if (childHeaders != null)
            headersFound++;
          else
            childHeaders = headers;
        }

        foreach (ProjectItem child in item.ProjectItems)
          headersFound += RemoveOrReplaceHeaderRecursive (child, childHeaders, searchForLicenseHeaders);
      }
      return headersFound;
    }

    /// <summary>
    /// Tries to open a given project item as a Document which can be used to add or remove headers.
    /// </summary>
    /// <param name="item">The project item.</param>
    /// <param name="document">The document which was created or null if an error occured (see return value).</param>
    /// <param name="headers">A dictionary of headers using the file extension as key and the header as value or null if headers should only be removed.</param>
    /// <returns>A value indicating the result of the operation. Document will be null unless DocumentCreated is returned.</returns>
    public CreateDocumentResult TryCreateDocument (ProjectItem item, out Document document, IDictionary<string, string[]> headers = null)
    {
      document = null;

      if (item.Kind != Constants.vsProjectItemKindPhysicalFile)
        return CreateDocumentResult.NoPhyiscalFile;

      //don't insert license header information in license header definitions
      if (item.Name.EndsWith (LicenseHeader.Extension))
        return CreateDocumentResult.LicenseHeaderDocument;

      //try to open the document as a text document
      try
      {
        if (!item.IsOpen[Constants.vsViewKindTextView])
          item.Open (Constants.vsViewKindTextView);
      }
      catch (COMException)
      {
        return CreateDocumentResult.NoTextDocument;
      }

      var itemDocument = item.Document;
      if (itemDocument == null)
        return CreateDocumentResult.NoPhyiscalFile;

      var textDocument = itemDocument.Object ("TextDocument") as TextDocument;
      if (textDocument == null)
        return CreateDocumentResult.NoTextDocument;

      //try to find a comment definitions for the language of the document
      var languagePage = _licenseHeaderExtension.GetLanguagesPage ();

      var language = languagePage.Languages
          .Where (x => x.Extensions.Any (y => item.Name.EndsWith (y, StringComparison.OrdinalIgnoreCase)))
          .FirstOrDefault();

      if (language == null)
        return CreateDocumentResult.LanguageNotFound;

      string[] header = null;
      if (headers != null)
      {
        var extension = headers.Keys
            .OrderBy (x => -x.Length)
            .Where (x => item.Name.EndsWith (x, StringComparison.OrdinalIgnoreCase))
            .FirstOrDefault();

        if (extension == null)
          return CreateDocumentResult.LanguageNotFound;

        header = headers[extension];

        if (header.All (string.IsNullOrWhiteSpace))
          return CreateDocumentResult.NoHeaderFound;
      }

      //get the required keywords from the options page
      var optionsPage = _licenseHeaderExtension.GetOptionsPage();

      document = new Document (
          textDocument,
          language,
          header,
          item,
          optionsPage.UseRequiredKeywords
              ? optionsPage.RequiredKeywords.Split (new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select (k => k.Trim())
              : null);

      return CreateDocumentResult.DocumentCreated;
    }
  }
}
