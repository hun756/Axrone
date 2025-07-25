import { DeepPartial } from '@axrone/utility';
import { TweenCore } from '../core';
import { TweenConfig } from '../types';
import { TypedArrayConstructor } from 'packages/utility/src/types';

export class ObjectTween<T extends object> extends TweenCore<T> {
    protected _valuesStartRepeat: DeepPartial<T> | null = null;
    protected _objectProps = new Set<string>();

    constructor(object: T, config?: TweenConfig<T>) {
        super(object, config);
    }

    protected _initStartEndValues(): void {
        this._collectProps(this._valuesEnd, '', this._objectProps);
        this._collectProps(this._valuesStart, '', this._objectProps);

        for (const prop of this._objectProps) {
            const propValue = this._getPropValue(this._object, prop);

            if (this._getPropValue(this._valuesStart, prop) === undefined) {
                this._setPropValue(this._valuesStart, prop, propValue);
            }

            if (this._getPropValue(this._valuesEnd, prop) === undefined) {
                this._setPropValue(this._valuesEnd, prop, propValue);
            }

            if (propValue === undefined) {
                this._setPropValue(this._object, prop, this._getPropValue(this._valuesStart, prop));
            }
        }

        this._valuesStartRepeat = this._deepClone(this._valuesStart);
    }

    protected _collectProps(obj: any, prefix: string, props: Set<string>): void {
        if (!obj || typeof obj !== 'object') return;

        for (const key in obj) {
            const value = obj[key];
            const propPath = prefix ? `${prefix}.${key}` : key;

            if (
                value !== null &&
                typeof value === 'object' &&
                !Array.isArray(value) &&
                !ArrayBuffer.isView(value)
            ) {
                this._collectProps(value, propPath, props);
            } else {
                props.add(propPath);
            }
        }
    }

    protected _getPropValue(obj: any, path: string): any {
        if (!obj) return undefined;

        const parts = path.split('.');
        let current = obj;

        for (const part of parts) {
            if (current === undefined || current === null) return undefined;
            current = current[part];
        }

        return current;
    }

    protected _setPropValue(obj: any, path: string, value: any): void {
        if (!obj) return;

        const parts = path.split('.');
        let current = obj;

        for (let i = 0; i < parts.length - 1; i++) {
            const part = parts[i];

            if (current[part] === undefined) {
                current[part] = {};
            }

            current = current[part];
        }

        const lastPart = parts[parts.length - 1];
        current[lastPart] = value;
    }

    protected _updateProperties(progress: number): void {
        for (const prop of this._objectProps) {
            const start = this._getPropValue(this._valuesStart, prop);
            const end = this._getPropValue(this._valuesEnd, prop);

            if (start === undefined || end === undefined) continue;

            if (Array.isArray(end) && Array.isArray(start)) {
                const result: number[] = [];
                
                for (let i = 0; i < end.length; i++) {
                    const startVal = i < start.length ? start[i] : 0;
                    result[i] = startVal + (end[i] - startVal) * progress;
                }

                this._setPropValue(this._object, prop, result);
            } else if (typeof end === 'number') {
                const startVal = typeof start === 'number' ? start : 0;
                const value = startVal + (end - startVal) * progress;
                this._setPropValue(this._object, prop, value);
            }
        }
    }

    protected _reset(): void {
        if (this._yoyo) {
            const tmp = this._valuesStart;
            this._valuesStart = this._valuesEnd;
            this._valuesEnd = tmp;
            this._reversed = !this._reversed;
        } else if (this._valuesStartRepeat) {
            this._valuesStart = this._deepClone(this._valuesStartRepeat);
            
            for (const prop of this._objectProps) {
                const startValue = this._getPropValue(this._valuesStart, prop);
                if (startValue !== undefined) {
                    this._setPropValue(this._object, prop, startValue);
                }
            }
        }
    }

    protected _deepClone<U>(source: U): U {
        if (source === null || source === undefined || typeof source !== 'object') {
            return source;
        }

        if (Array.isArray(source)) {
            return source.map((item) => this._deepClone(item)) as unknown as U;
        }

        if (ArrayBuffer.isView(source)) {
            const constructor = source.constructor as TypedArrayConstructor;
            return new constructor(source as any) as unknown as U;
        }

        if (source instanceof Date) {
            return new Date(source.getTime()) as unknown as U;
        }

        if (source instanceof Map) {
            const result = new Map();
            source.forEach((value, key) => {
                result.set(key, this._deepClone(value));
            });
            return result as unknown as U;
        }

        if (source instanceof Set) {
            const result = new Set();
            for (const value of source) {
                result.add(this._deepClone(value));
            }
            return result as unknown as U;
        }

        const result = Object.create(null);
        for (const key in source) {
            if (Object.prototype.hasOwnProperty.call(source, key)) {
                result[key] = this._deepClone((source as any)[key]);
            }
        }

        return result as U;
    }
}
