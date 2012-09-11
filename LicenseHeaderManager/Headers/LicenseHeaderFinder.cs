﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using EnvDTE;

namespace LicenseHeaderManager.Headers
{
  /// <summary>
  /// Class for finding the nearest license header file for a ProjectItem or a Project
  /// </summary>
  public class LicenseHeaderFinder
  {
    /// <summary>
    /// Lookup the license header file within a project or a projectitem, if there is no onw on this level, moving up to next level.
    /// </summary>
    /// <param name="projectOrItem">An oject which is either is a project or a projectitem</param>
    /// <returns>A dictionary, which contains the extensions and the corresponding lines</returns>
    public static IDictionary<string, string[]> GetHeaderRecursive (object projectOrItem)
    {
      var parentAsProject = projectOrItem as Project;
      if (parentAsProject != null)
        return GetHeader (parentAsProject); //We are on the top --> load project License file if it exists

      var parentAsProjectItem = projectOrItem as ProjectItem;
      if (parentAsProjectItem != null)
        return GetHeaderRecursive (parentAsProjectItem); //Lookup in the item above

      return null;
    }

    /// <summary>
    /// Lookup the license header file within a project , if there is no onw on this level, moving up to next level.
    /// </summary>
    /// <param name="projectItem"></param>
    /// <returns>A dictionary, which contains the extensions and the corresponding lines</returns>
    public static IDictionary<string, string[]> GetHeaderRecursive (ProjectItem projectItem)
    {
      //Check for License-file within this level
      var headerFile = GetLicenseHeaderDefinitions (projectItem.ProjectItems);
      if (!string.IsNullOrEmpty(headerFile))
        return LoadLicenseHeaderDefinition (headerFile); //Found a License header file on this level
      //Lookup in the parent --> Go Up!
      return GetHeaderRecursive (projectItem.Collection.Parent);
    }

    /// <summary>
    /// Lookup the license header file within the given projectItems.
    /// </summary>
    /// <param name="projectItems"></param>
    /// <returns>A dictionary, which contains the extensions and the corresponding lines</returns>
    public static IDictionary<string, string[]> GetHeader (ProjectItems projectItems)
    {
      //Check for License-file within this level
      var headerFile = GetLicenseHeaderDefinitions (projectItems);
      if (!string.IsNullOrEmpty (headerFile))
        return LoadLicenseHeaderDefinition (headerFile); //Found a License header file on this level
      return null;
    }

    /// <summary>
    /// Returns the License header file for the specified project
    /// </summary>
    /// <param name="project">The project which is scanned for the License header file</param>
    /// <returns>A dictionary, which contains the extensions and the corresponding lines</returns>
    public static IDictionary<string, string[]> GetHeader (Project project)
    {
      var headerFile = GetLicenseHeaderDefinitions (project.ProjectItems);
      var definition = LoadLicenseHeaderDefinition (headerFile);
      return definition;
    }

    #region Helper Methods
    
    private static Dictionary<string, string[]> LoadLicenseHeaderDefinition (string headerFilePath)
    {
      if (string.IsNullOrEmpty (headerFilePath))
        return null;
      var headers = new Dictionary<string, string[]> ();
      AddHeaders (headers, headerFilePath);
      return headers;
    }

    private static void AddHeaders (IDictionary<string, string[]> headers, string headerFilePath)
    {
      IEnumerable<string> extensions = null;
      IList<string> header = new List<string> ();

      var streamreader = new StreamReader (headerFilePath, true);
      while (!streamreader.EndOfStream)
      {
        var line = streamreader.ReadLine ();
        if (line.StartsWith (LicenseHeader.Keyword))
        {
          if (extensions != null)
          {
            var array = header.ToArray ();
            foreach (var extension in extensions)
              headers[extension] = array;
          }
          extensions = line.Substring (LicenseHeader.Keyword.Length).Split (new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Select (LicenseHeader.AddDot);
          header.Clear ();
        }
        else
          header.Add (line);
      }

      if (extensions != null)
      {
        var array = header.ToArray ();
        foreach (var extension in extensions)
          headers[extension] = array;
      }
    }


    private static string GetLicenseHeaderDefinitions (ProjectItems projectItems)
    {
      if (projectItems == null)
        return null;
      foreach (ProjectItem item in projectItems)
      {
        if (item.FileCount == 1)
        {
          string fileName = null;
          
          try
          {
            fileName = item.FileNames[0];
          }
          catch (Exception) 
          {
          }

          if (fileName != null && Path.GetExtension (fileName).ToLower () == LicenseHeader.Extension)
            return fileName;
        }
      }
      return string.Empty;
    }
    #endregion

  }
}
