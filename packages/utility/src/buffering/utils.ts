import { ByteOrder } from './types';

export class BufferUtils {
    private static readonly textEncoder = new TextEncoder();
    private static readonly textDecoder = new TextDecoder();
    private static crc32Table: Uint32Array | null = null;

    static nativeEndianness(): ByteOrder {
        const buffer = new ArrayBuffer(2);
        new DataView(buffer).setInt16(0, 1, true);
        return new Int16Array(buffer)[0] === 1 ? ByteOrder.Little : ByteOrder.Big;
    }

    static getCrc32Table(): Uint32Array {
        if (BufferUtils.crc32Table === null) {
            const table = new Uint32Array(256);

            for (let i = 0; i < 256; i++) {
                let crc = i;
                for (let j = 0; j < 8; j++) {
                    crc = crc & 1 ? 0xedb88320 ^ (crc >>> 1) : crc >>> 1;
                }
                table[i] = crc;
            }

            BufferUtils.crc32Table = table;
        }

        return BufferUtils.crc32Table;
    }

    static encodeString(str: string): Uint8Array {
        return BufferUtils.textEncoder.encode(str);
    }

    static decodeString(bytes: Uint8Array): string {
        return BufferUtils.textDecoder.decode(bytes);
    }

    static calculateHash(data: Uint8Array): number {
        const fnvPrime = 0x01000193;
        let h = 0x811c9dc5;

        for (let i = 0; i < data.length; i++) {
            h ^= data[i];
            h = Math.imul(h, fnvPrime);
        }

        return h >>> 0;
    }

    static calculateCrc32(data: Uint8Array): number {
        let crc = 0xffffffff;
        const table = BufferUtils.getCrc32Table();

        for (let i = 0; i < data.length; i++) {
            crc = (crc >>> 8) ^ table[(crc ^ data[i]) & 0xff];
        }

        return (crc ^ 0xffffffff) >>> 0;
    }
}
