using System;

namespace Antinet {
	internal static unsafe class DynamicCrc32 {
		private static readonly uint[] _table;

		static DynamicCrc32() {
			uint seed;

			_table = new uint[256];
			seed = (uint)new Random().Next();
			// 默认为0xEDB88320，我们随机生成一个，防止从内存中搜索到CRC32校验值
			for (int i = 0; i < 256; i++) {
				uint crc;

				crc = (uint)i;
				for (int j = 8; j > 0; j--) {
					if ((crc & 1) == 1)
						crc = (crc >> 1) ^ seed;
					else
						crc >>= 1;
				}
				_table[i] = crc;
			}
		}

		public static uint Compute(byte[] data) {
			if (data == null)
				throw new ArgumentNullException(nameof(data));

			uint crc32;

			crc32 = 0xFFFFFFFF;
			for (int i = 0; i < data.Length; i++)
				crc32 = (crc32 >> 8) ^ _table[(crc32 ^ data[i]) & 0xFF];
			return ~crc32;
		}

		public static uint Compute(byte* pData, uint length) {
			uint crc32;

			crc32 = 0xFFFFFFFF;
			for (uint i = 0; i < length; i++)
				crc32 = (crc32 >> 8) ^ _table[(crc32 ^ pData[i]) & 0xFF];
			return ~crc32;
		}
	}
}
