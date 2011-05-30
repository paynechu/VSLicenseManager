﻿using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.Win32;
using LicenseHeaderManager.Headers;
using LicenseHeaderManager.Options;
using Constants = EnvDTE.Constants;
using Document = LicenseHeaderManager.Headers.Document;
using System.Reflection;
using System.Windows;

namespace LicenseHeaderManager
{
  /// <summary>
  /// This is the class that implements the package exposed by this assembly.
  ///
  /// The minimum requirement for a class to be considered a valid package for Visual Studio
  /// is to implement the IVsPackage interface and register itself with the shell.
  /// This package uses the helper classes defined inside the Managed Package Framework (MPF)
  /// to do it: it derives from the Package class that provides the implementation of the 
  /// IVsPackage interface and uses the registration attributes defined in the framework to 
  /// register itself and its components with the shell.
  /// </summary>
  // This attribute tells the PkgDef creation utility (CreatePkgDef.exe) that this class is
  // a package.
  [PackageRegistration (UseManagedResourcesOnly = true)]
  // This attribute is used to register the informations needed to show the this package
  // in the Help/About dialog of Visual Studio.
  [InstalledProductRegistration ("#110", "#112", "0.9", IconResourceID = 400)]
  // This attribute is needed to let the shell know that this package exposes some menus.
  [ProvideMenuResource ("Menus.ctmenu", 1)]
  [ProvideOptionPage (typeof (OptionsPage), "License Headers", "General", 0, 0, true)]
  [ProvideOptionPage (typeof (LanguagesPage), "License Headers", "Languages", 0, 0, true)]
  [ProvideProfile (typeof (OptionsPage), "License Headers", "General", 0, 0, true)]
  [ProvideProfile (typeof (LanguagesPage), "License Headers", "Languages", 0, 0, true)]
  [ProvideAutoLoad (VSConstants.UICONTEXT.SolutionOpening_string)]
  [Guid (GuidList.guidLicenseHeadersPkgString)]
  public sealed class LicenseHeadersPackage : Package
  {
    /// <summary>
    /// Default constructor of the package.
    /// Inside this method you can place any initialization code that does not require 
    /// any Visual Studio service because at this point the package object is created but 
    /// not sited yet inside Visual Studio environment. The place to do all the other 
    /// initialization is the Initialize method.
    /// </summary>
    public LicenseHeadersPackage ()
    {
    }

    private DTE2 _dte;
    private CommandEvents _events;
    private OleMenuCommand _addLicenseHeaderCommand;
    private OleMenuCommand _removeLicenseHeaderCommand;

    /// <summary>
    /// Initialization of the package; this method is called right after the package is sited, so this is the place
    /// where you can put all the initilaization code that rely on services provided by VisualStudio.
    /// </summary>
    protected override void Initialize ()
    {
      base.Initialize();

      _dte = GetService (typeof (DTE)) as DTE2;

      OleMenuCommandService mcs = GetService (typeof (IMenuCommandService)) as OleMenuCommandService;
      if (mcs != null)
      {
        _addLicenseHeaderCommand = AddCommand (mcs, PkgCmdIDList.cmdIdAddLicenseHeader, AddLicenseHeaderCallback);
        _addLicenseHeaderCommand.BeforeQueryStatus += QueryCommandStatus;
        _removeLicenseHeaderCommand = AddCommand (mcs, PkgCmdIDList.cmdIdRemoveLicenseHeader, RemoveLicenseHeaderCallback);

        AddCommand (mcs, PkgCmdIDList.cmdIdAddLicenseHeadersToAllFiles, AddLicenseHeadersToAllFilesCallback);
        AddCommand (mcs, PkgCmdIDList.cmdIdRemoveLicenseHeadersFromAllFiles, RemoveLicenseHeadersFromAllFilesCallback);
        AddCommand (mcs, PkgCmdIDList.cmdIdAddLicenseHeaderDefinitionFile, AddLicenseHeaderDefinitionFileCallback);
        AddCommand (mcs, PkgCmdIDList.cmdIdAddExistingLicenseHeaderDefinitionFile, AddExistingLicenseHeaderDefinitionFileCallback);
        AddCommand (mcs, PkgCmdIDList.cmdIdLicenseHeaderOptions, LicenseHeaderOptionsCallback);
      }

      var page = (OptionsPage) GetDialogPage (typeof (OptionsPage));

      if (page != null)
      {
        if (page.AttachToCommand)
        {
          _events = _dte.Events.CommandEvents[page.AttachedCommandGuid, page.AttachedCommandId];
          _events.BeforeExecute += AttachedCommandExecuted;
        }

        page.OptionsChanged += (s, e) =>
        {
          if (_events != null)
          {
            _events.BeforeExecute -= AttachedCommandExecuted;
            _events = null;
          }

          if (page.AttachToCommand)
          {
            _events = _dte.Events.CommandEvents[page.AttachedCommandGuid, page.AttachedCommandId];
            _events.BeforeExecute += AttachedCommandExecuted;
          }
        };
      }
    }

    private OleMenuCommand AddCommand (OleMenuCommandService service, uint id, EventHandler handler)
    {
      var commandId = new CommandID (GuidList.guidLicenseHeadersCmdSet, (int) id);
      var command = new OleMenuCommand (handler, commandId);
      service.AddCommand (command);
      return command;
    }

    private void QueryCommandStatus (object sender, EventArgs e)
    {
      bool visible = false;

      var item = GetActiveProjectItem ();
      if (item != null)
      {
        Document document;
        visible = TryCreateDocument (item, out document) == CreateDocumentResult.DocumentCreated;
      }

      _addLicenseHeaderCommand.Visible = visible;
      _removeLicenseHeaderCommand.Visible = visible;
    }

    private void AttachedCommandExecuted (string Guid, int ID, object CustomIn, object CustomOut, ref bool CancelDefault)
    {
      _addLicenseHeaderCommand.Invoke();
    }

    private void AddLicenseHeaderCallback (object sender, EventArgs e)
    {
      RemoveOrReplaceHeader (false);
    }

    private void RemoveLicenseHeaderCallback (object sender, EventArgs e)
    {
      RemoveOrReplaceHeader (true);
    }

    private void RemoveOrReplaceHeader (bool removeOnly)
    {
      var item = GetActiveProjectItem ();
      if (item != null)
      {
        try
        {
          var headers = removeOnly ? null : LicenseHeader.GetLicenseHeaders (item.ContainingProject);

          Document document;
          CreateDocumentResult result = TryCreateDocument (item, out document, headers);
          switch (result)
          {
            case CreateDocumentResult.DocumentCreated:
              document.ReplaceHeaderIfNecessary ();
              break;
            case CreateDocumentResult.LanguageNotFound:
              string message = string.Format (Resources.Error_LanguageNotFound, Path.GetExtension (item.Name)).Replace(@"\n", "\n");
              if (MessageBox.Show (message, Resources.Error, MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes)
                ShowOptionPage (typeof (LanguagesPage));
              break;
            case CreateDocumentResult.NoHeaderFound:
              message = string.Format (Resources.Error_NoHeaderFound, Path.GetExtension (item.Name)).Replace (@"\n", "\n");
              if (MessageBox.Show (message, Resources.Error, MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes)
                AddLicenseHeaderDefinitionFile ();
              break;
          }
        }
        catch (ArgumentException ex)
        {
          MessageBox.Show (ex.Message, Resources.Error, MessageBoxButton.OK, MessageBoxImage.Warning);
        }
      }
    }

    private void AddLicenseHeadersToAllFilesCallback (object sender, EventArgs e)
    {
      var project = GetActiveProject ();
      if (project != null)
      {
        var headers = LicenseHeader.GetLicenseHeaders (project);

        if (headers.Count == 0)
        {
          string message = Resources.Error_NoHeaderDefinition.Replace (@"\n", "\n");
          if (MessageBox.Show (message, Resources.Error, MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes)
            AddLicenseHeaderDefinitionFile ();
        }
        else
        {
          foreach (ProjectItem item in project.ProjectItems)
            RemoveOrReplaceHeaderRecursive (item, headers);
        }
      }
    }

    private void RemoveLicenseHeadersFromAllFilesCallback (object sender, EventArgs e)
    {
      var project = GetActiveProject ();
      if (project != null)
      {
        foreach (ProjectItem item in project.ProjectItems)
          RemoveOrReplaceHeaderRecursive (item, null);
      }
    }

    private void RemoveOrReplaceHeaderRecursive (ProjectItem item, IDictionary<string, string[]> headers)
    {
      bool isOpen = item.IsOpen[Constants.vsViewKindAny];
      bool isSaved = item.Saved;

      Document document;
      if (TryCreateDocument (item, out document, headers) == CreateDocumentResult.DocumentCreated)
      {
        document.ReplaceHeaderIfNecessary ();
        if (isOpen)
        {
          if (isSaved)
            item.Save ();
        }
        else
          item.Document.Close (vsSaveChanges.vsSaveChangesYes);

      }
      foreach (ProjectItem child in item.ProjectItems)
        RemoveOrReplaceHeaderRecursive (child, headers);
    }

    private void AddLicenseHeaderDefinitionFileCallback (object sender, EventArgs e)
    {
      AddLicenseHeaderDefinitionFile();
    }

    private void AddLicenseHeaderDefinitionFile ()
    {
      var project = GetActiveProject();
      if (project != null)
      {
        var fileName = LicenseHeader.GetNewFileName (project);
        var item = _dte.ItemOperations.AddNewItem ("General\\Text File", fileName);
        
        using (var resource = Assembly.GetExecutingAssembly ().GetManifestResourceStream ("Rubicon.LicenseHeaders.default.licenseheader"))
        {
          var text = item.Document.Object ("TextDocument") as TextDocument;
          if (text != null)
          {
            text.CreateEditPoint().Insert (new StreamReader (resource).ReadToEnd());
            item.Save ();
          }
        }
      }
    }

    private void AddExistingLicenseHeaderDefinitionFileCallback (object sender, EventArgs e)
    {
      var project = GetActiveProject();
      if (project != null)
      {
        FileDialog dialog = new OpenFileDialog();
        dialog.CheckFileExists = true;
        dialog.CheckPathExists = true;
        dialog.DefaultExt = LicenseHeader.Cextension;
        dialog.DereferenceLinks = true;
        dialog.Filter = "License Header Definitions|*" + LicenseHeader.Cextension;
        dialog.InitialDirectory = Path.GetDirectoryName (project.FileName);

        bool? result = dialog.ShowDialog();
        if (result.HasValue && result.Value)
          project.ProjectItems.AddFromFile (dialog.FileName);
      }
    }

    private void LicenseHeaderOptionsCallback (object sender, EventArgs e)
    {
      ShowOptionPage (typeof (OptionsPage));
    }

    private CreateDocumentResult TryCreateDocument (ProjectItem item, out Document document, IDictionary<string, string[]> headers = null)
    {
      document = null;

      if (item.Kind != Constants.vsProjectItemKindPhysicalFile)
        return CreateDocumentResult.NoPhyiscalFile;

      if (item.Name.EndsWith (LicenseHeader.Cextension))
        return CreateDocumentResult.LicenseHeaderDocument;

      item.Open (Constants.vsViewKindTextView);

      var textDocument = item.Document.Object ("TextDocument") as TextDocument;
      if (textDocument == null)
        return CreateDocumentResult.NoTextDocument;
      
      var languagePage = (LanguagesPage) GetDialogPage (typeof (LanguagesPage));
      var extensions = from l in languagePage.Languages
                       from e in l.Extensions
                       where item.Name.ToLower().EndsWith (e)
                       orderby e.Length descending // ".designer.cs" has a higher priority then ".cs"
                       select new { Extension = e, Language = l };

      if (!extensions.Any ())
        return CreateDocumentResult.LanguageNotFound;

      Options.Language language = null;

      string[] header = null;

      //if headers is null, we only want to remove the existing headers and thus don't need to find the right header
      if (headers != null)
      {
        //try to find a header for each of the languages (if there's no header for ".designer.cs", use the one for ".cs" files
        foreach (var extension in extensions)
        {
          if (headers.TryGetValue (extension.Extension, out header))
          {
            language = extension.Language;
            break;
          }
        }

        if (header == null)
          return CreateDocumentResult.NoHeaderFound;
      }
      else
        language = extensions.First ().Language;

      var optionsPage = (OptionsPage) GetDialogPage (typeof (OptionsPage));

      document = new Document (
        textDocument,
        language,
        header,
        optionsPage.UseRequiredKeywords ? optionsPage.RequiredKeywords.Split (new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(k => k.Trim()) : null);

      return CreateDocumentResult.DocumentCreated;
    }

    private ProjectItem GetActiveProjectItem()
    {
      try
      {
        var activeDocument = _dte.ActiveDocument;
        if (activeDocument == null)
          return null;
        else
          return activeDocument.ProjectItem;
      }
      catch (ArgumentException)
      {
        return null;
      }
    }

    private Project GetActiveProject ()
    {
      var projects = _dte.ActiveSolutionProjects as object[];
      if (projects != null && projects.Length == 1)
        return projects[0] as Project;
      else
        return null;
    }
  }
}