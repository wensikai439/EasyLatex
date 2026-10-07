# Third-party software

EasyLatex source code is licensed under MIT. The following independent components are used or may be distributed with the application.

- **AvalonEdit 6.3.1.120** — MIT, copyright AvalonEdit contributors. https://github.com/icsharpcode/AvalonEdit/blob/master/LICENSE
- **Microsoft .NET 10 runtime / Windows SDK projections** — MIT and associated third-party licenses. Runtime notices are retained in published distributions. https://github.com/dotnet/runtime/blob/main/LICENSE.TXT and https://github.com/dotnet/runtime/blob/main/THIRD-PARTY-NOTICES.TXT
- **Tectonic 0.17.0** — MIT with third-party TeX/XeTeX, font and native-library notices. https://github.com/tectonic-typesetting/tectonic/blob/master/LICENSE and https://github.com/tectonic-typesetting/tectonic/tree/master/docs
- **Windows native PDF renderer** — an operating-system component; not redistributed by EasyLatex.

TeX Live and MiKTeX are detected and invoked when installed by the user. They are not bundled. Tectonic downloads required TeX resources from its official bundle and caches them; their individual licenses apply.

TeXMini, Texmaker, TeXworks, and Texpile informed product research and design. Their source code is not incorporated into this application.

Release packaging must retain the actual license and notice files supplied by each redistributed component, including the .NET runtime and Tectonic dependency notices. This overview does not replace those license texts.
