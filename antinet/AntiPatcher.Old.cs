//using System;
//using System.Collections.Generic;
//using System.IO;
//using System.Runtime.InteropServices;
//using System.Text;
//using static Antinet.AntiPatcher.NativeMethods;

//namespace Antinet {
//	/// <summary>
//	/// 反补丁类
//	/// </summary>
//	public static unsafe class AntiPatcher {
//		private static PEInfo _clrPEInfo;
//		private static IMAGE_SECTION_HEADER* _pClrTextSectionHeader;
//		private static uint _clrTextSectionSize;
//		private static uint _clrTextSectionCrc32Original;
//		private static bool _isInitialized;

//		static AntiPatcher() {
//			Initialize();
//		}

//		public static bool CheckCLR() {
//			byte[] textSection;
//			byte* pTextSection;
//			uint crc32;

//			textSection = new byte[_clrTextSectionSize];
//			pTextSection = (byte*)_clrPEInfo.PEImage + _pClrTextSectionHeader->VirtualAddress;
//			for (int i = 0; i < textSection.Length; i++)
//				textSection[i] = pTextSection[i];
//			for (int i = 0; i < 0x2000; i++)
//				textSection[i] = 0;
//			//crc32 = DynamicCrc32.Compute((byte*)_clrPEInfo.PEImage + _pClrTextSectionHeader->VirtualAddress, _clrTextSectionSize);
//			crc32 = DynamicCrc32.Compute(textSection);
//			File.WriteAllBytes("memory.bin", textSection);
//			return crc32 == _clrTextSectionCrc32Original;
//		}

//		private static void Initialize() {
//			void* clrModuleHandle;
//			StringBuilder stringBuilder;
//			byte[] clrFile;

//			if (_isInitialized)
//				return;
//			switch (Environment.Version.Major) {
//			case 2:
//				clrModuleHandle = GetModuleHandle("mscorwks.dll");
//				break;
//			case 4:
//				clrModuleHandle = GetModuleHandle("clr.dll");
//				break;
//			default:
//				throw new NotSupportedException();
//			}
//			if (clrModuleHandle == null)
//				throw new InvalidOperationException();
//			_clrPEInfo = new PEInfo(clrModuleHandle);
//			_pClrTextSectionHeader = _clrPEInfo.FindSectionHeader(".text");
//			if (_pClrTextSectionHeader == null)
//				throw new InvalidOperationException();
//			stringBuilder = new StringBuilder((int)MAX_PATH);
//			if (!GetModuleFileName(clrModuleHandle, stringBuilder, MAX_PATH))
//				throw new InvalidOperationException();
//			clrFile = File.ReadAllBytes(stringBuilder.ToString());
//			// 读取CLR模块文件内容
//			fixed (byte* pPEImage = clrFile) {
//				PEInfo peInfo;
//				IMAGE_SECTION_HEADER* pTextSectionHeader;
//				byte[] textSection;

//				peInfo = new PEInfo(pPEImage);
//				PerformFileBaseRelocation(peInfo, (IMAGE_BASE_RELOCATION*)(pPEImage + peInfo.ToFOA(peInfo.RelocationTableDirectory->VirtualAddress)), (void*)((long)_clrPEInfo.PEImage - (long)_clrPEInfo.ImageBase), !peInfo.IsPE32);
//				pTextSectionHeader = peInfo.FindSectionHeader(".text");
//				_clrTextSectionSize = AlignUp(pTextSectionHeader->VirtualSize, peInfo.SectionAlignment);
//				textSection = new byte[_clrTextSectionSize];
//				// 分配内存
//				for (uint i = 0; i < pTextSectionHeader->VirtualSize; i++)
//					textSection[i] = *(pPEImage + pTextSectionHeader->PointerToRawData + i);
//				// 复制.text节内容
//				for (int i = 0; i < 0x2000; i++)
//					// .text节前面不知道为什么被修改了
//					textSection[i] = 0;
//				File.WriteAllBytes("file.bin", textSection);
//				_clrTextSectionCrc32Original = DynamicCrc32.Compute(textSection);
//				// 计算出原始的crc32值用于校验
//			}
//			_isInitialized = true;
//		}

//		private static uint AlignUp(uint value, uint alignment) {
//			return (value + alignment - 1) & ~(alignment - 1);
//		}

//		private static bool PerformFileBaseRelocation(PEInfo peInfo, IMAGE_BASE_RELOCATION* relocation, void* delta, bool isPE64) {
//			if (delta == null)
//				return true;

//			byte* codeBase = (byte*)peInfo.PEImage;

//			while (relocation->VirtualAddress > 0) {
//				uint i;
//				byte* dest = codeBase + peInfo.ToFOA(relocation->VirtualAddress);
//				ushort* relInfo = (ushort*)OffsetPointer(relocation, (void*)IMAGE_BASE_RELOCATION.UnmanagedSize);
//				for (i = 0; i < ((relocation->SizeOfBlock - IMAGE_BASE_RELOCATION.UnmanagedSize) / 2); i++, relInfo++) {
//					// the upper 4 bits define the type of relocation
//					uint type = (uint)*relInfo >> 12;
//					// the lower 12 bits define the offset
//					uint offset = (uint)*relInfo & 0xfff;

//					switch (type) {
//					case IMAGE_REL_BASED_ABSOLUTE:
//						// skip relocation
//						break;

//					case IMAGE_REL_BASED_HIGHLOW:
//						// change complete 32 bit address
//						uint* patchAddrHL = (uint*)(dest + offset);
//						*patchAddrHL += (uint)delta;
//						break;
//					case IMAGE_REL_BASED_DIR64:
//						if (isPE64) {
//							ulong* patchAddr64 = (ulong*)(dest + offset);
//							*patchAddr64 += (ulong)delta;
//						}
//						break;
//					default:
//						break;
//					}
//				}

//				// advance to next relocation block
//				relocation = (IMAGE_BASE_RELOCATION*)OffsetPointer(relocation, (void*)relocation->SizeOfBlock);
//			}
//			return true;
//		}

//		private static void* OffsetPointer(void* data, void* offset) {
//			return (void*)((ulong)data + (ulong)offset);
//		}

//		public static void Test() {
//			Initialize();
//		}

//		internal sealed class PEInfo {
//			private readonly void* _pPEImage;
//			private readonly uint _sectionsCount;
//			private readonly bool _isPE32;
//			private readonly ulong _imageBase;
//			private readonly uint _sectionAlignment;
//			private readonly IMAGE_DATA_DIRECTORY* _pRelocationTableDirectory;
//			private readonly IMAGE_SECTION_HEADER* _pSectionHeaders;

//			public void* PEImage => _pPEImage;

//			public uint SectionsCount => _sectionsCount;

//			public bool IsPE32 => _isPE32;

//			public ulong ImageBase => _imageBase;

//			public uint SectionAlignment => _sectionAlignment;

//			public IMAGE_DATA_DIRECTORY* RelocationTableDirectory => _pRelocationTableDirectory;

//			public IMAGE_SECTION_HEADER* SectionHeaders => _pSectionHeaders;

//			public PEInfo(void* pPEImage) {
//				byte* p;
//				ushort optionalHeaderSize;

//				_pPEImage = pPEImage;
//				p = (byte*)pPEImage;
//				p += *(uint*)(p + 0x3C);
//				// NtHeader
//				p += 4 + 2;
//				// 跳过 Signature + Machine
//				_sectionsCount = *(ushort*)p;
//				p += 2 + 4 + 4 + 4;
//				// 跳过 NumberOfSections + TimeDateStamp + PointerToSymbolTable + NumberOfSymbols
//				optionalHeaderSize = *(ushort*)p;
//				p += 2 + 2;
//				// 跳过 SizeOfOptionalHeader + Characteristics
//				_isPE32 = *(ushort*)p == 0x010B;
//				_imageBase = _isPE32 ? *(uint*)(p + 0x1C) : *(ulong*)(p + 0x18);
//				_sectionAlignment = *(uint*)(p + 0x20);
//				_pRelocationTableDirectory = (IMAGE_DATA_DIRECTORY*)(p + (_isPE32 ? 0x88 : 0x98));
//				p += optionalHeaderSize;
//				// 跳过 OptionalHeader
//				_pSectionHeaders = (IMAGE_SECTION_HEADER*)p;
//			}

//			public IMAGE_SECTION_HEADER* FindSectionHeader(string name) {
//				byte[] temp;
//				byte[] nameBytes;

//				temp = Encoding.UTF8.GetBytes(name);
//				if (temp.Length > 8)
//					throw new ArgumentOutOfRangeException(nameof(name));
//				nameBytes = new byte[8];
//				for (int i = 0; i < temp.Length; i++)
//					nameBytes[i] = temp[i];
//				for (uint i = 0; i < _sectionsCount; i++)
//					if (BytesEquals(_pSectionHeaders[i].Name, nameBytes))
//						return _pSectionHeaders + i;
//				return null;
//			}

//			private static bool BytesEquals(byte* x, byte[] y) {
//				for (int i = 0; i < y.Length; i++)
//					if (x[i] != y[i])
//						return false;
//				return true;
//			}

//			public uint ToFOA(uint rva) {
//				for (uint i = 0; i < _sectionsCount; i++)
//					if (rva >= _pSectionHeaders[i].VirtualAddress && rva < _pSectionHeaders[i].VirtualAddress + Math.Max(_pSectionHeaders[i].VirtualSize, _pSectionHeaders[i].SizeOfRawData))
//						return rva - _pSectionHeaders[i].VirtualAddress + _pSectionHeaders[i].PointerToRawData;
//				return rva;
//			}
//		}

//		internal static class NativeMethods {
//			public const uint MAX_PATH = 260;
//			public const uint IMAGE_REL_BASED_ABSOLUTE = 0;
//			public const uint IMAGE_REL_BASED_HIGHLOW = 3;
//			public const uint IMAGE_REL_BASED_DIR64 = 10;

//			[StructLayout(LayoutKind.Sequential)]
//			public struct IMAGE_DATA_DIRECTORY {
//				public uint VirtualAddress;
//				public uint Size;
//			}

//			[StructLayout(LayoutKind.Sequential)]
//			public struct IMAGE_SECTION_HEADER {
//				public fixed byte Name[8];
//				public uint VirtualSize;
//				public uint VirtualAddress;
//				public uint SizeOfRawData;
//				public uint PointerToRawData;
//				public uint PointerToRelocations;
//				public uint PointerToLinenumbers;
//				public ushort NumberOfRelocations;
//				public ushort NumberOfLinenumbers;
//				public uint Characteristics;
//			}

//			[StructLayout(LayoutKind.Sequential)]
//			public struct IMAGE_BASE_RELOCATION {
//				public static readonly uint UnmanagedSize = (uint)sizeof(IMAGE_BASE_RELOCATION);

//				public uint VirtualAddress;
//				public uint SizeOfBlock;
//			}

//			[DllImport("kernel32.dll", BestFitMapping = false, CharSet = CharSet.Unicode, SetLastError = true)]
//			public static extern void* GetModuleHandle(string lpModuleName);

//			[DllImport("kernel32.dll", BestFitMapping = false, CharSet = CharSet.Unicode, SetLastError = true)]
//			[return: MarshalAs(UnmanagedType.Bool)]
//			public static extern bool GetModuleFileName(void* hModule, StringBuilder lpFilename, uint nSize);
//		}
//	}
//}
