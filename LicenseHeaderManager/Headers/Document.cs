﻿#region copyright
// Copyright (c) 2011 rubicon IT GmbH

// Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"),
// to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense,
// and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

// The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS
// FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY,
// WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE. 
#endregion
using System;
using System.Collections.Generic;
using System.Linq;
using EnvDTE;
using Language = LicenseHeaderManager.Options.Language;
using System.Text.RegularExpressions;

namespace LicenseHeaderManager.Headers
{
  public class Document
  {
    internal readonly DocumentHeader _header;
    internal readonly Language _language;
    internal readonly IEnumerable<string> _keywords;
    internal readonly TextDocument _document;
    internal readonly CommentParser _commentParser;
    internal readonly string _lineEndingInDocument;

    public Document (TextDocument document, Language language, string[] lines, ProjectItem projectItem, IEnumerable<string> keywords = null)
    {
      _document = document;

      _lineEndingInDocument = NewLineManager.DetectMostFrequentLineEnd (GetText ());


      string inputText = CreateInputText(lines);
      _header = new DocumentHeader (document, inputText, new DocumentHeaderProperties (projectItem));
      _keywords = keywords;

      _language = language;
      
      _commentParser = new CommentParser (language.LineComment, language.BeginComment, language.EndComment, language.BeginRegion, language.EndRegion);
    }

    private string CreateInputText (string[] lines)
    {
      if (lines == null)
        return null;

      var inputText = string.Join (_lineEndingInDocument, lines);
      if (!inputText.EndsWith (_lineEndingInDocument))
        inputText += _lineEndingInDocument;
      return inputText;
    }

    public bool ValidateHeader ()
    {
      if (_header.IsEmpty)
        return true;
      else
        return LicenseHeader.Validate (_header.Text, _commentParser);
    }

    private string GetText (TextPoint start, TextPoint end)
    {
      return _document.CreateEditPoint (start).GetText (end);
    }

    private string GetText ()
    {
      return GetText (_document.StartPoint, _document.EndPoint);
    }

    private string GetExistingHeader ()
    {
      string header = _commentParser.Parse (GetText ());

      if (_keywords == null || _keywords.Any (k => header.ToLower ().Contains (k.ToLower ())))
        return header;
      else
        return string.Empty;
    }

    public void ReplaceHeaderIfNecessary ()
    {
      var skippedText = SkipText ();
      if (!string.IsNullOrEmpty (skippedText))
        RemoveHeader (skippedText);

      string existingHeader = GetExistingHeader ();

      if (!_header.IsEmpty)
      {
        if (existingHeader != _header.Text)
          ReplaceHeader (existingHeader, PrepareHeader(_header.Text));
      }
      else
        RemoveHeader (existingHeader);

      if (!string.IsNullOrEmpty (skippedText))
        AddHeader (PrepareHeader(skippedText));
    }

    private string SkipText ()
    {
      if (string.IsNullOrEmpty (_language.SkipExpression))
        return null;
      var match = Regex.Match (GetText (), _language.SkipExpression, RegexOptions.IgnoreCase);
      if (match.Success && match.Index == 0)
        return match.Value;
      else
        return null;
    }

    private void ReplaceHeader (string existingHeader, string newHeader)
    {
      RemoveHeader (existingHeader);
      AddHeader (newHeader);
    }

    private void AddHeader (string header)
    {
      if (!string.IsNullOrEmpty (header))
      {
        var start = _document.CreateEditPoint (_document.StartPoint);
        start.Insert (header);
      }
    }

    private string PrepareHeader(string header)
    {
      var headerWithNewLine = header;
      var newLine = NewLineManager.DetectMostFrequentLineEnd (headerWithNewLine);
      //if the header ends with an empty line, there's no need to insert one
      if (!headerWithNewLine.EndsWith (newLine))
        headerWithNewLine += newLine;

      headerWithNewLine = NewLineManager.ReplaceAllLineEnds (headerWithNewLine, _lineEndingInDocument);
      int lastNewLine = headerWithNewLine.LastIndexOf (newLine, header.Length - newLine.Length);
      if (lastNewLine < 0 || !string.IsNullOrWhiteSpace (header.Substring (lastNewLine, header.Length - lastNewLine)))
      {
        //if there's a comment right at the beginning of the file,
        //we need to add an empty line so that the comment doesn't
        //become a part of the header
        if (!string.IsNullOrEmpty (_commentParser.Parse (GetText())))
          headerWithNewLine += newLine;
      }

      return headerWithNewLine;
    }

    private EditPoint EndOfHeader (string header, TextPoint start = null)
    {
      if (start == null)
        start = _document.CreateEditPoint (_document.StartPoint);
      var end = _document.CreateEditPoint (start);

      var headerLengthInCursorSteps = NewLineManager.ReplaceAllLineEnds(header, " ").Length;
      end.CharRight (headerLengthInCursorSteps);

      return end;
    }

    private void RemoveHeader (string header)
    {
      if (!string.IsNullOrEmpty (header))
      {
        var start = _document.CreateEditPoint (_document.StartPoint);
        var end = EndOfHeader (header, _document.StartPoint);
        start.Delete (end);
      }
    }
  }
}