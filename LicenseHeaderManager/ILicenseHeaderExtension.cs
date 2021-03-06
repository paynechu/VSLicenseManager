﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LicenseHeaderManager.Options;

namespace LicenseHeaderManager
{
  public interface ILicenseHeaderExtension
  {
    void ShowLanguagesPage ();

    IDefaultLicenseHeaderPage DefaultLicenseHeaderPage { get; }
    ILanguagesPage LanguagesPage { get; }
    IOptionsPage OptionsPage { get; }
  }
}
