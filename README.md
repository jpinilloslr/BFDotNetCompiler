BF compiler
=========
This program compiles BF source code into .Net executable files. It translates the program into IL code and uses ilasm.exe to generate the executable.

About BF
---------------

[Brainfuck](http://en.wikipedia.org/wiki/Brainfuck) is an esoteric programming language created in 1993 by Urban Müller, and notable for its extreme minimalism. While it is fully Turing-complete, it is not intended for practical use, but to challenge and amuse programmers.

Prerequesite
------------

You will need ilasm.exe to generate the .Net executable file from the IL code. This tool gets installed with Visual Studio.

Usage
-----

`bfc source` 

`source` should be the name of the program source without the "txt" extension. You can find some examples under `TestPrograms` directory.
