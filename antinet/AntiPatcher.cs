using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using static Antinet.AntiPatcher.NativeMethods;

namespace Antinet {
	/// <summary>
	/// 反补丁类
	/// </summary>
	public static unsafe class AntiPatcher {
		private static void* _clrModuleHandle;
		private static uint _clrPEHeaderCrc32Original;
		private static bool _isInitialized;

		static AntiPatcher() {
			Initialize();
		}

		/// <summary>
		/// 检查CLR模块的PE头是否被修改
		/// </summary>
		/// <returns>如果被修改，返回 <see langword="true"/></returns>
		public static bool VerifyClrPEHeader() {
			return DynamicCrc32.Compute(CopyPEHeader(_clrModuleHandle)) != _clrPEHeaderCrc32Original;
		}

		private static void Initialize() {
			StringBuilder stringBuilder;
			byte[] clrFile;

			if (_isInitialized)
				return;
			switch (Environment.Version.Major) {
			case 2:
				_clrModuleHandle = GetModuleHandle("mscorwks.dll");
				break;
			case 4:
				_clrModuleHandle = GetModuleHandle("clr.dll");
				break;
			default:
				throw new NotSupportedException();
			}
			if (_clrModuleHandle == null)
				throw new InvalidOperationException();
			stringBuilder = new StringBuilder((int)MAX_PATH);
			if (!GetModuleFileName(_clrModuleHandle, stringBuilder, MAX_PATH))
				throw new InvalidOperationException();
			clrFile = File.ReadAllBytes(stringBuilder.ToString());
			fixed (byte* pPEImage = clrFile)
				_clrPEHeaderCrc32Original = DynamicCrc32.Compute(CopyPEHeader(pPEImage));
			_isInitialized = true;
		}

		private static byte[] CopyPEHeader(void* pPEImage) {
			uint imageBaseOffset;
			uint length;
			byte[] peHeader;

			GetPEInfo(pPEImage, out imageBaseOffset, out length);
			peHeader = new byte[length];
			fixed (byte* pPEHeader = peHeader) {
				for (uint i = 0; i < length; i++)
					pPEHeader[i] = ((byte*)pPEImage)[i];
				// 复制PE头
				*(void**)(pPEHeader + imageBaseOffset) = null;
				// 清除可选头的ImageBase字段，这个字段会变化，不能用于校验
			}
			return peHeader;
		}

		private static void GetPEInfo(void* pPEImage, out uint imageBaseOffset, out uint length) {
			byte* p;
			ushort optionalHeaderSize;
			bool isPE32;
			uint sectionsCount;
			void* pSectionHeaders;

			p = (byte*)pPEImage;
			p += *(uint*)(p + 0x3C);
			// NtHeader
			p += 4 + 2;
			// 跳过 Signature + Machine
			sectionsCount = *(ushort*)p;
			p += 2 + 4 + 4 + 4;
			// 跳过 NumberOfSections + TimeDateStamp + PointerToSymbolTable + NumberOfSymbols
			optionalHeaderSize = *(ushort*)p;
			p += 2 + 2;
			// 跳过 SizeOfOptionalHeader + Characteristics
			isPE32 = *(ushort*)p == 0x010B;
			imageBaseOffset = isPE32 ? (uint)(p + 0x1C - (byte*)pPEImage) : (uint)(p + 0x18 - (byte*)pPEImage);
			p += optionalHeaderSize;
			// 跳过 OptionalHeader
			pSectionHeaders = (void*)p;
			length = (uint)((byte*)pSectionHeaders + 0x28 * sectionsCount - (byte*)pPEImage);
		}

		internal static class NativeMethods {
			public const uint MAX_PATH = 260;

			[DllImport("kernel32.dll", BestFitMapping = false, CharSet = CharSet.Unicode, SetLastError = true)]
			public static extern void* GetModuleHandle(string lpModuleName);

			[DllImport("kernel32.dll", BestFitMapping = false, CharSet = CharSet.Unicode, SetLastError = true)]
			[return: MarshalAs(UnmanagedType.Bool)]
			public static extern bool GetModuleFileName(void* hModule, StringBuilder lpFilename, uint nSize);
		}
	}
}
