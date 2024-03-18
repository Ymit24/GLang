#!/bin/bash
nasm -f elf32 -o compiled_code.o compiled.nasm
gcc -m32 compiled_code.o -o compiled_code -no-pie
./compiled_code
