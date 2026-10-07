namespace EasyLatex;

public static class Templates
{
    public const string Article = """
% !TeX program = xelatex
\documentclass[11pt]{article}
\usepackage[a4paper,margin=2.5cm]{geometry}
\usepackage{amsmath,amssymb}
\usepackage[colorlinks=true,linkcolor=blue]{hyperref}

\title{A small beginning}
\author{Your name}
\date{\today}

\begin{document}
\maketitle

\begin{abstract}
Every good paper starts with one clear idea.
\end{abstract}

\section{Introduction}
Welcome to EasyLatex. Write on the left, see the PDF on the right.
Press Ctrl+Enter to save and compile.

\section{A little mathematics}
\begin{equation}
  e^{i\pi}+1=0.
\end{equation}

\section{Next steps}
Add your ideas here. Use the outline to move between sections.
The optional AI assistant can help with a selected passage or a compile error.

\end{document}
""";
    public const string Chinese = """
% !TeX program = xelatex
\documentclass[UTF8,fontset=fandol,11pt]{ctexart}
\usepackage[a4paper,margin=2.5cm]{geometry}
\usepackage{amsmath,amssymb}
\usepackage[colorlinks=true]{hyperref}
\title{从一个想法开始}
\author{你的名字}
\date{\today}
\begin{document}
\maketitle
\begin{abstract}
在这里简要介绍研究问题、方法和主要发现。
\end{abstract}
\section{引言}
欢迎使用 EasyLatex。左侧写作，右侧查看 PDF。
按 Ctrl+Enter 保存并编译，按 Ctrl+J 定位到 PDF。
\section{方法}
\begin{equation}
  E=mc^2.
\end{equation}
\section{结论}
在这里写下你的结论。
\end{document}
""";
    public const string Beamer = """
% !TeX program = xelatex
\documentclass{beamer}
\usetheme{default}
\usepackage{fontspec}
\setsansfont{lmsans10-regular.otf}
\setmainfont{lmroman10-regular.otf}
\usefonttheme{professionalfonts}
\definecolor{easyaccent}{RGB}{104,84,204}
\setbeamercolor{structure}{fg=easyaccent}
\setbeamertemplate{navigation symbols}{}
\usepackage{amsmath}
\title{One idea, clearly told}
\author{Your name}
\date{\today}
\begin{document}
\begin{frame}
  \titlepage
\end{frame}
\begin{frame}{The idea}
  \begin{itemize}
    \item A question worth asking.
    \item A method that makes sense.
    \item A result worth sharing.
  \end{itemize}
\end{frame}
\end{document}
""";
}
