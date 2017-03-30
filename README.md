# Sharp80

TRS80 Emulator for Windows

This is a full-featured emulator of a TRS-80 Model III circa 1981. It includes:

* Complete and self contained with ROM and DOS built in
* Faithful Z-80 CPU emulation (including undocumented opcodes -- passes all ZEXALL opcode tests)
* Runs at standard 2.03MHz (or run up to 100MHz on a modern PC)
* Bundled disk and tape library includes applications, utilities, operating systems, and games
* Supports up to four virtual floppy drives, and all major virtual floppy formats (DMK, JV3, JV1)
* Tape drive emulation supports high and low speed reading and writing (CAS format)
* Windowed and full-screen modes
* Built-in Z-80 assembler and disassembler
* Real-time monitor of Z-80 CPU internals and IO device status
* Printer to file support
* Support for all video modes, including wide characters and Kanji mode

Project Objectives

* Run old TRS-80 programs for fun and general interest
* Document the internal workings of this ground-breaking 8-bit machine
* Provide examples of emulation techniques for others to use

To develop with this code, you'll need:

* The latest MS Visual Studio 2017 (the free community
edition works fine)
* The SharpDX library (download the 3.1.0 SDK from sharpdx.org) and then put the dll files into the
/SharpDX directory under the Sharp80.exe file.

This code is licensed under the GPL v3.

Read more at http://www.sharp80.com.
