eazdevirt
=========

eazdevirt is a toolkit for inspecting and devirtualizing executables that have
been protected with [Eazfuscator.NET]'s virtualization feature.

eazdevirt uses [dnlib] to read and write assemblies, which is included as a
submodule.

Features
========

* Identify all methods (stubs) which have been virtualized
* Devirtualize methods for which all virtual opcodes are understood
* Identify and extract the embedded resource file containing virtualization info
* List all virtual opcodes and indicate which are identified

Common Issues / Solutions
=========================

Resolution of Types / Methods
-----------------------------

Because of how [Eazfuscator.NET]'s virtual machine works, resolving some types
and methods requires that their names and MDTokens be as expected (more
specifically, to match what is found in the embedded resource file). This means
that running [de4dot] against an executable with the default options before
attempting to devirtualize said executable might cause certain types/methods to
not resolve correctly.

One way around this is the following:

```sh
de4dot --dont-rename --keep-types --preserve-table all MyAssembly.exe
eazdevirt devirtualize MyAssembly-cleaned.exe
de4dot MyAssembly-cleaned-devirtualized.exe
```

... leaving the result as MyAssembly-cleaned-devirtualized-cleaned.exe

Building
========

Mono
----

To build with Mono:

```sh
git submodule update --init
xbuild eazdevirt.sln
```

MSVS
----

On a Windows machine with MSVS installed, opening the solution file and
building in Visual Studio should be sufficient (after updating the submodule
as shown above).

`msbuild eazdevirt.sln` should also work.


[de4dot]:https://github.com/0xd4d/de4dot
[dnlib]:https://github.com/0xd4d/dnlib
[Eazfuscator.NET]:https://www.gapotchenko.com/eazfuscator.net
