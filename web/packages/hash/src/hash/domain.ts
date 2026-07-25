import type { IDomainSeparation } from './interfaces';
import type { HashValue, HashAlgorithmName } from './types';
import { createHasher } from './factory';
import type { IHasher } from './interfaces';
import { HashDomainTooLongError } from './errors';

const MAX_TAG_LENGTH = 256;

export class DomainSeparatedHasher<H extends HashValue> implements IDomainSeparation {
    readonly tag: string;
    private _tagBytes: Uint8Array;
    private _hasher: IHasher<H>;
    private _algorithm: HashAlgorithmName;

    constructor(algorithm: HashAlgorithmName, tag: string) {
        if (tag.length > MAX_TAG_LENGTH) {
            throw new HashDomainTooLongError(`Domain tag length ${tag.length} exceeds maximum ${MAX_TAG_LENGTH}`);
        }
        this._algorithm = algorithm;
        this.tag = tag;
        this._tagBytes = new TextEncoder().encode(tag);
        this._hasher = createHasher<H>(algorithm);
        this._hasher.updateBytes(this._tagBytes);
    }

    hash<H2 extends HashValue = H>(data: Uint8Array): H2 {
        this._hasher.updateBytes(data);
        return this._hasher.digest() as unknown as H2;
    }

    hashString<H2 extends HashValue = H>(input: string): H2 {
        this._hasher.updateString(input);
        return this._hasher.digest() as unknown as H2;
    }

    reset(): this {
        this._hasher.reset();
        this._hasher.updateBytes(this._tagBytes);
        return this;
    }

    withTag(tag: string): IDomainSeparation {
        return new DomainSeparatedHasher<H>(this._algorithm, tag);
    }
}

export function createDomainSeparator<H extends HashValue>(algorithm: HashAlgorithmName, tag: string): DomainSeparatedHasher<H> {
    return new DomainSeparatedHasher<H>(algorithm, tag);
}
