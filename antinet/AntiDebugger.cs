using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using static Antinet.AntiDebugger.NativeMethods;

namespace Antinet {
	/// <summary>
	/// 反调试类
	/// </summary>
	public static unsafe class AntiDebugger {
		private static bool _isManagedDebuggerPrevented;
		private static bool _isManagedInitialized;
		private static byte* _pIsDebuggerAttached;
		private static uint _isDebuggerAttachedLength;
		private static uint _isDebuggerAttachedCrc32;

		/// <summary>
		/// 阻止托管调试器调试当前进程。
		/// </summary>
		/// <returns></returns>
		public static bool PreventManagedDebugger() {
			if (_isManagedDebuggerPrevented)
				return true;
			_isManagedDebuggerPrevented = AntiManagedDebugger.Initialize();
			return _isManagedDebuggerPrevented;
		}

		/// <summary>
		/// 检查是否存在任意类型调试器。
		/// </summary>
		/// <returns></returns>
		public static bool HasDebugger() {
			return HasUnmanagedDebugger() || HasManagedDebugger();
			// 检查是否存在非托管调试器的速度更快，效率更高，在CLR40下也能检测到托管调试器。
		}

		/// <summary>
		/// 检查是否存在非托管调试器。
		/// 在CLR20下，使用托管调试器调试进程，此方法返回 <see langword="false"/>，因为CLR20没有使用正常调试流程，Win32函数检测不到调试器。
		/// 在CLR40下，使用托管调试器调试进程，此方法返回 <see langword="true"/>。
		/// </summary>
		/// <returns></returns>
		public static bool HasUnmanagedDebugger() {
			bool isDebugged;

			if (IsDebuggerPresent())
				return true;
			if (!CheckRemoteDebuggerPresent(GetCurrentProcess(), &isDebugged))
				return true;
			if (isDebugged)
				return true;
			try {
				CloseHandle((void*)0xDEADC0DE);
			}
			catch {
				return true;
			}
			return false;
		}

		/// <summary>
		/// 使用 <see cref="Debugger.IsAttached"/> 检查是否存在托管调试器。
		/// 注意，此方法不能检测到非托管调试器（如OllyDbg，x64dbg）的存在。
		/// </summary>
		/// <returns></returns>
		public static bool HasManagedDebugger() {
			byte[] opcodes;
			byte* pCodeStart;
			byte* pCodeCurrent;
			byte* pCodeEnd;
			ldasm_data ldasmData;
			bool is64Bit;

			InitializeManaged();
			if (Debugger.IsAttached)
				// 此时肯定有托管调试器附加
				return true;
			// 此时不能保证托管调试器未调试当前进程
			if (_pIsDebuggerAttached[0] == 0x33 && _pIsDebuggerAttached[1] == 0xC0 && _pIsDebuggerAttached[2] == 0xC3)
				// 这是dnSpy反反调试的特征
				return true;
			// 有可能特征变了，进一步校验
			opcodes = new byte[_isDebuggerAttachedLength];
			pCodeStart = _pIsDebuggerAttached;
			pCodeCurrent = pCodeStart;
			pCodeEnd = _pIsDebuggerAttached + _isDebuggerAttachedLength;
			is64Bit = sizeof(void*) == 8;
			while (true) {
				uint length;

				length = Ldasm.ldasm(pCodeCurrent, &ldasmData, is64Bit);
				if ((ldasmData.flags & Ldasm.F_INVALID) != 0)
					throw new NotSupportedException();
				CopyOpcode(&ldasmData, pCodeCurrent, opcodes, (uint)(pCodeCurrent - pCodeStart));
				pCodeCurrent += length;
				if (pCodeCurrent == pCodeEnd)
					break;
			}
			// 复制Opcodes
			if (DynamicCrc32.Compute(opcodes) != _isDebuggerAttachedCrc32)
				// 如果CRC32不相等，那说明CLR可能被Patch了
				return true;
			return false;
		}

		private static void InitializeManaged() {
			void* clrModuleHandle;
			StringBuilder stringBuilder;
			byte[] clrFile;

			if (_isManagedInitialized)
				return;
			switch (Environment.Version.Major) {
			case 2:
				_pIsDebuggerAttached = (byte*)typeof(Debugger).GetMethod("IsDebuggerAttached", BindingFlags.NonPublic | BindingFlags.Static).MethodHandle.GetFunctionPointer();
				clrModuleHandle = GetModuleHandle("mscorwks.dll");
				break;
			case 4:
				_pIsDebuggerAttached = (byte*)typeof(Debugger).GetMethod("get_IsAttached").MethodHandle.GetFunctionPointer();
				// Debugger.IsAttached的get属性是一个有[MethodImpl(MethodImplOptions.InternalCall)]特性的方法，意思是实现在CLR内部，而且没有任何stub，直接指向CLR内部。
				// 通过x64dbg调试，可以知道Debugger.get_IsAttached()对应clr!DebugDebugger::IsDebuggerAttached()。
				clrModuleHandle = GetModuleHandle("clr.dll");
				break;
			default:
				throw new NotSupportedException();
			}
			if (clrModuleHandle == null)
				throw new InvalidOperationException();
			stringBuilder = new StringBuilder((int)MAX_PATH);
			if (!GetModuleFileName(clrModuleHandle, stringBuilder, MAX_PATH))
				throw new InvalidOperationException();
			clrFile = File.ReadAllBytes(stringBuilder.ToString());
			// 读取CLR模块文件内容
			fixed (byte* pPEImage = clrFile) {
				PEInfo peInfo;
				uint isDebuggerAttachedRva;
				uint isDebuggerAttachedFoa;
				byte* pCodeStart;
				byte* pCodeCurrent;
				ldasm_data ldasmData;
				bool is64Bit;
				byte[] opcodes;

				peInfo = new PEInfo(pPEImage);
				isDebuggerAttachedRva = (uint)(_pIsDebuggerAttached - (byte*)clrModuleHandle);
				isDebuggerAttachedFoa = peInfo.ToFOA(isDebuggerAttachedRva);
				pCodeStart = pPEImage + isDebuggerAttachedFoa;
				pCodeCurrent = pCodeStart;
				is64Bit = sizeof(void*) == 8;
				opcodes = new byte[0x200];
				// 分配远大于实际函数大小的内存
				while (true) {
					uint length;

					length = Ldasm.ldasm(pCodeCurrent, &ldasmData, is64Bit);
					if ((ldasmData.flags & Ldasm.F_INVALID) != 0)
						throw new NotSupportedException();
					CopyOpcode(&ldasmData, pCodeCurrent, opcodes, (uint)(pCodeCurrent - pCodeStart));
					if (*pCodeCurrent == 0xC3) {
						// 找到了第一个ret指令
						pCodeCurrent += length;
						break;
					}
					pCodeCurrent += length;
				}
				// 复制Opcode直到出现第一个ret
				_isDebuggerAttachedLength = (uint)(pCodeCurrent - pCodeStart);
				fixed (byte* pOpcodes = opcodes)
					_isDebuggerAttachedCrc32 = DynamicCrc32.Compute(pOpcodes, _isDebuggerAttachedLength);
			}
			_isManagedInitialized = true;
		}

		private static void CopyOpcode(ldasm_data* pLdasmData, void* pCode, byte[] opcodes, uint offset) {
			for (byte i = 0; i < pLdasmData->opcd_size; i++)
				opcodes[offset + pLdasmData->opcd_offset + i] = ((byte*)pCode)[pLdasmData->opcd_offset + i];
		}

		internal sealed class PEInfo {
			private readonly void* _pPEImage;
			private readonly uint _sectionsCount;
			private readonly IMAGE_SECTION_HEADER* _pSectionHeaders;

			public void* PEImage => _pPEImage;

			public uint SectionsCount => _sectionsCount;

			public IMAGE_SECTION_HEADER* SectionHeaders => _pSectionHeaders;

			public PEInfo(void* pPEImage) {
				byte* p;
				ushort optionalHeaderSize;

				_pPEImage = pPEImage;
				p = (byte*)pPEImage;
				p += *(uint*)(p + 0x3C);
				// NtHeader
				p += 4 + 2;
				// 跳过 Signature + Machine
				_sectionsCount = *(ushort*)p;
				p += 2 + 4 + 4 + 4;
				// 跳过 NumberOfSections + TimeDateStamp + PointerToSymbolTable + NumberOfSymbols
				optionalHeaderSize = *(ushort*)p;
				p += 2 + 2;
				// 跳过 SizeOfOptionalHeader + Characteristics
				p += optionalHeaderSize;
				// 跳过 OptionalHeader
				_pSectionHeaders = (IMAGE_SECTION_HEADER*)p;
			}

			public uint ToFOA(uint rva) {
				for (uint i = 0; i < _sectionsCount; i++)
					if (rva >= _pSectionHeaders[i].VirtualAddress && rva < _pSectionHeaders[i].VirtualAddress + Math.Max(_pSectionHeaders[i].VirtualSize, _pSectionHeaders[i].SizeOfRawData))
						return rva - _pSectionHeaders[i].VirtualAddress + _pSectionHeaders[i].PointerToRawData;
				return rva;
			}
		}

		internal static class NativeMethods {
			public const uint MAX_PATH = 260;

			[StructLayout(LayoutKind.Sequential)]
			public struct IMAGE_SECTION_HEADER {
				public fixed byte Name[8];
				public uint VirtualSize;
				public uint VirtualAddress;
				public uint SizeOfRawData;
				public uint PointerToRawData;
				public uint PointerToRelocations;
				public uint PointerToLinenumbers;
				public ushort NumberOfRelocations;
				public ushort NumberOfLinenumbers;
				public uint Characteristics;
			}

			[DllImport("kernel32.dll", BestFitMapping = false, CharSet = CharSet.Unicode, SetLastError = true)]
			[return: MarshalAs(UnmanagedType.Bool)]
			public static extern bool IsDebuggerPresent();

			[DllImport("kernel32.dll", BestFitMapping = false, CharSet = CharSet.Unicode, SetLastError = true)]
			public static extern void* GetCurrentProcess();

			[DllImport("kernel32.dll", BestFitMapping = false, CharSet = CharSet.Unicode, SetLastError = true)]
			[return: MarshalAs(UnmanagedType.Bool)]
			public static extern bool CheckRemoteDebuggerPresent(void* hProcess, bool* pbDebuggerPresent);

			[DllImport("kernel32.dll", BestFitMapping = false, CharSet = CharSet.Unicode, SetLastError = true)]
			[return: MarshalAs(UnmanagedType.Bool)]
			public static extern bool CloseHandle(void* hObject);

			[DllImport("kernel32.dll", BestFitMapping = false, CharSet = CharSet.Unicode, SetLastError = true)]
			public static extern void* GetModuleHandle(string lpModuleName);

			[DllImport("kernel32.dll", BestFitMapping = false, CharSet = CharSet.Unicode, SetLastError = true)]
			[return: MarshalAs(UnmanagedType.Bool)]
			public static extern bool GetModuleFileName(void* hModule, StringBuilder lpFilename, uint nSize);
		}
	}
}
