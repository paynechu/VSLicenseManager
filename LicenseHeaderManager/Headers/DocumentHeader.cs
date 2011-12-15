﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using EnvDTE;
using System.IO;

namespace LicenseHeaderManager.Headers
{
  internal class DocumentHeader
  {
    private readonly TextDocument _document;
    private readonly string _text;
    private readonly FileInfo _fileInfo;
    private readonly IEnumerable<DocumentHeaderProperty> _properties;

    public DocumentHeader(TextDocument document, string[] rawLines, IEnumerable<DocumentHeaderProperty> properties)
    {
      if (document == null) throw new ArgumentNullException("document");
      if (properties == null) throw new ArgumentNullException("properties");

      _document = document;
      _properties = properties;

      _fileInfo = CreateFileInfo();
      _text = CreateText(rawLines);
    }

    private FileInfo CreateFileInfo()
    {
      string pathToDocument = _document.Parent.FullName;

      if (File.Exists(pathToDocument))
      {
        return new FileInfo(pathToDocument);
      }
      return null;
    }

    private string CreateText(string[] rawLines)
    {
      if (rawLines == null)
      {
        return null;
      }

      string rawText = string.Join(Environment.NewLine, rawLines);
      string finalText = CreateFinalText(rawText);
      return finalText;
    }

    private string CreateFinalText(string rawText)
    {
      string pathToDocument = _document.Parent.FullName;

      string finalText = rawText;

      foreach (DocumentHeaderProperty property in _properties)
      {
        if (property.CanCreateValue(this))
        {
          finalText = finalText.Replace(property.Token, property.CreateValue(this));
        }
      }

      return finalText;
    }

    public bool IsEmpty
    {
      get { return _text == null; }
    }

    public FileInfo FileInfo
    {
      get { return _fileInfo; }
    }

    public string Text
    {
      get { return _text; }
    }
  }
}
