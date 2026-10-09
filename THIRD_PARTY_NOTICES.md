# Third-party software

The original EasyLatex components in this version are proprietary. The following independent components are used or may be distributed with the application; their licenses are unchanged and remain applicable independently.

- **AvalonEdit 6.3.1.120** — MIT, copyright AvalonEdit contributors. https://github.com/icsharpcode/AvalonEdit/blob/master/LICENSE
- **DiffPlex 1.9.0** — Apache-2.0, textual difference algorithms. License retained in `licenses/DiffPlex-Apache-2.0.txt`. https://github.com/mmanela/diffplex
- **YDotNet 0.6.0 / YDotNet.Native.Win32 0.6.0** — MIT, .NET bindings and the packaged Windows Yrs native library. License texts are retained in `licenses/YDotNet-MIT.txt` and `licenses/Yrs-MIT.txt`. https://github.com/y-crdt/ydotnet and https://github.com/y-crdt/y-crdt
- **Microsoft .NET 10 runtime / Windows SDK projections** — MIT and associated third-party licenses. Runtime notices are retained in published distributions. https://github.com/dotnet/runtime/blob/main/LICENSE.TXT and https://github.com/dotnet/runtime/blob/main/THIRD-PARTY-NOTICES.TXT
- **Tectonic 0.17.0** — MIT with third-party TeX/XeTeX, font and native-library notices. https://github.com/tectonic-typesetting/tectonic/blob/master/LICENSE and https://github.com/tectonic-typesetting/tectonic/tree/master/docs
- **Windows native PDF renderer** — an operating-system component; not redistributed by EasyLatex.

Complete TeX Live and MiKTeX distributions are detected and invoked when installed by the user; they are not bundled. The portable package includes an unmodified, prewarmed subset of Tectonic's official TeX resource bundle for the five built-in templates. Additional resources are downloaded on demand and cached. Original package license comments and individual licenses remain applicable.

The resource subset includes LaTeX/LPPL packages, AMS fonts, Latin Modern fonts under the GUST Font License, and Fandol fonts under GPL with the font exception. Their supplied license and README files are retained in `licenses/`, together with TeX Live and CTAN licensing guidance. Upstream packages and corresponding font sources can be obtained from [CTAN](https://ctan.org/pkg/fandol), [Latin Modern](https://ctan.org/pkg/lm), and [Tectonic's bundles](https://github.com/tectonic-typesetting/tectonic-bundles). The EasyLatex proprietary notice does not replace these licenses.

TeXMini, Texmaker, TeXworks, and Texpile informed product research and design. Their source code is not incorporated into this application.

Release packaging must retain the actual license and notice files supplied by each redistributed component, including the .NET runtime and Tectonic dependency notices. This overview does not replace those license texts.
