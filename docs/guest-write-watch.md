<!--
Copyright (C) 2026 ZylxEmu Emulator Project
SPDX-License-Identifier: GPL-2.0-or-later
-->

# Guest write watch

`GuestWriteWatch` is an optional diagnostic tool. It helps you find managed
code and HLE code that damage guest memory. The tool starts only if you set one
or more `ZYLXEMU_WATCH_*` environment variables.

The tool monitors writes through the ZylxEmu managed virtual-memory APIs. It
does not monitor stores that native guest code makes directly. Use a platform
debugger or a hardware watchpoint to monitor these stores.

## Watch modes

- `ZYLXEMU_WATCH_WRITE=0x<address>` logs a write that overlaps the eight-byte
  block at the specified guest address.
- `ZYLXEMU_WATCH_POOL_HEADER=1` monitors the pointer at offset `0x40`. It
  monitors the first 64 direct mappings that have a size of 64 KiB and
  protection value `0xF2`.
- `ZYLXEMU_WATCH_VALUE_PATTERN=1` logs an eight-byte write if its lower 32 bits
  are `1`. The upper 32 bits must look like a small guest-pointer prefix.
- `ZYLXEMU_WATCH_VALUE1=1` logs short writes of value `1` in the high guest
  memory range. The tool logs a maximum of 128 entries for each process.
- `ZYLXEMU_WATCH_BULK_TORN=1` scans aligned 64-bit words in bulk writes. It
  finds damaged pointer patterns and byte-shifted pointer patterns. The tool
  logs a maximum of 64 entries for each process.
- `ZYLXEMU_WATCH_BULK_DEST_HI=0x<high-dword>` scans only writes that have the
  specified upper 32 bits in the destination address.

For each match, the tool logs the destination address, the data pattern, and the
managed call stack. The log uses the `watch_write` or `watch_bulk_torn` warning
tag.

Use these variables together to scan bulk writes in the
`0x00000080xxxxxxxx` region.

macOS and Linux:

```sh
ZYLXEMU_WATCH_BULK_TORN=1 \
ZYLXEMU_WATCH_BULK_DEST_HI=0x80 \
ZylxEmu /path/to/eboot.bin
```

Windows PowerShell:

```powershell
$env:ZYLXEMU_WATCH_BULK_TORN = "1"
$env:ZYLXEMU_WATCH_BULK_DEST_HI = "0x80"
& .\ZylxEmu.exe C:\path\to\game\eboot.bin
```

To reduce unnecessary log entries, use an exact `ZYLXEMU_WATCH_WRITE`
address from a crash dump.
