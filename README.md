Code to prevent a debugger from working
=============================================================

It uses undocumented features of Microsoft's CLR to prevent managed debuggers from working. It's possible that a future version of Microsoft's CLR will be updated so this code will either not be able to prevent the managed debugger from working or even cause unexpected behaviors.

Tested versions of the CLR:

* CLR 2.0.50727.42 (.NET 2.0 RTM) x86 and x64
* CLR 2.0.50727.5466 (latest .NET 3.5 release as of today) x86 and x64
* CLR 4.0.30319.1 (.NET 4.0 RTM) x86 and x64
* CLR 4.0.30319.17020 (.NET 4.5 Dev Preview) x86 and x64
* CLR 4.0.30319.17929 (.NET 4.5 RTM) x86 and x64

CLR 2.0 is used by .NET Framework 2.0 - 3.5. CLR 4.0 is used by .NET Framework 4.0 - 4.5.

There are four test executables, one for each combination of CLR major version and x86/x64 architecture. You can try attaching a managed debugger at various points, eg. at startup, after the program has started but before it has initialized the anti-managed debugger code, and after it has initialized the anti-managed debugger code.

Managed debuggers you can try:

* Visual Studio's .NET debugger
* [mdbg](https://www.microsoft.com/en-us/download/details.aspx?id=2282)

Anti-managed debugger
=====================

Most anti-managed debugger code will call `System.Diagnostics.Debugger.IsAttached` somewhere in `Main()` to check whether a managed debugger is present. This code doesn't do that. Instead, it prevents any managed .NET debugger from working by killing the .NET debugger thread. When this thread is killed, no managed .NET debugger can get any debug messages and will fail to work.

Note that it doesn't prevent non-managed debuggers from working (eg. `WinDbg` or `OllyDbg`). Non-managed debuggers can't debug managed code the way a managed debugger can. Debugging managed code using a non-managed debugger is not easy.

Technical details
-----------------

When the CLR starts, it creates a debugger class instance (called `Debugger`). This class will create a `DebuggerRCThread` instance which is the .NET debugger thread. This thread is only killed when the CLR exits. To exit this thread, one must clear its "keep-looping" instance field, and signal its event to wake it up.

Both of these instances are saved somewhere in the `.data` section.

In order to find the interesting `DebuggerRCThread` instance, we must scan the `.data` section for the `Debugger` instance pointer. The reason I chose to find this one first is that it contains the current `pid` which makes finding it a little easier. When we've found something that appears to be the `Debugger` instance and it has the `pid` in the correct location, we get the pointer to the `DebuggerRCThread` instance.

The `DebuggerRCThread` instance also has a pointer back to the `Debugger` instance. If it matches, then we can be very sure that we've found both of them.

Once we have the `DebuggerRCThread` instance, it's trivial to clear the keep-looping variable and signal the event so it wakes up and exits.

To prevent a debugger from attaching, one can clear the debugger IPC block's size field. If this is not an expected value, `CordbProcess::VerifyControlBlock()` in `mscordbi.dll` will return an error and no debugger is able to attach.
