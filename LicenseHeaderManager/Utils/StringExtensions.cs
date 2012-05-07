﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace LicenseHeaderManager.Utils
{
  internal static class StringExtensions
  {
    internal static int CountOccurrence(this string inputString, string searchString)
    {
      int idx = 0;
      int count = 0;
      while ((idx = inputString.IndexOf (searchString, idx)) != -1)
      {
        idx += searchString.Length;
        count++;
      }
      return count;
    }
  }
}
