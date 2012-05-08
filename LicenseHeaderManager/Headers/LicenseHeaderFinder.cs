﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using EnvDTE;

namespace LicenseHeaderManager.Headers
{
  public class LicenseHeaderFinder
  {
    public static IDictionary<string, string[]> GetHeaderRecursive (object projectOrItem)
    {
      var parentAsProject = projectOrItem as Project;
      if (parentAsProject != null)
        return GetHeaders (parentAsProject); //We are on the top --> load project License file if it exists

      var parentAsProjectItem = projectOrItem as ProjectItem;
      if (parentAsProjectItem != null)
        return GetHeadersRecursive (parentAsProjectItem); //Lookup in the item above
      return null;
    }
    
    public static IDictionary<string, string[]> GetHeadersRecursive (ProjectItem projectItem)
    {
      //Check for License-file within this level
      var headerFile = GetLicenseHeaderDefinitions (projectItem.ProjectItems);
      if (!string.IsNullOrEmpty(headerFile))
        return LoadLicenseHeaderDefinition (headerFile); //Found a License header file on this level
      //Lookup in the parent --> Go Up!
      return GetHeaderRecursive (projectItem.Collection.Parent);
    }

    public static IDictionary<string, string[]> GetHeaders (ProjectItem projectItem)
    {
      //Check for License-file within this level
      var headerFile = GetLicenseHeaderDefinitions (projectItem.ProjectItems);
      if (!string.IsNullOrEmpty (headerFile))
        return LoadLicenseHeaderDefinition (headerFile); //Found a License header file on this level
      return null;
    }

    /// <summary>
    /// Returns the License header file for the specified project
    /// </summary>
    /// <param name="project">The project which is scanned for the License header file</param>
    /// <returns>A dictionary, which contains the extensions and the corresponding lines</returns>
    public static IDictionary<string, string[]> GetHeaders (Project project)
    {
      var headerFile = GetLicenseHeaderDefinitions (project.ProjectItems);
      var definition = LoadLicenseHeaderDefinition (headerFile);
      return definition;
    }

    private static Dictionary<string, string[]> LoadLicenseHeaderDefinition (string headerFile)
    {
      if (string.IsNullOrEmpty (headerFile))
        return null;
      var headers = new Dictionary<string, string[]> ();
      AddHeaders (headers, headerFile);
      return headers;
    }

    private static void AddHeaders (IDictionary<string, string[]> headers, string definition)
    {
      IEnumerable<string> extensions = null;
      IList<string> header = new List<string> ();
      foreach (var line in File.ReadAllLines (definition, Encoding.Default))
      {
        if (line.StartsWith (LicenseHeader.c_keyword))
        {
          if (extensions != null)
          {
            var array = header.ToArray ();
            foreach (var extension in extensions)
              headers[extension] = array;
          }
          extensions = line.Substring (LicenseHeader.c_keyword.Length).Split (new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Select (LicenseHeader.AddDot);
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

          if (fileName != null && Path.GetExtension (fileName).ToLower () == LicenseHeader.Cextension)
            return fileName;
        }
      }
      return string.Empty;
    }

  }
}
