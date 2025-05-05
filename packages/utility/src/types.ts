export type Primitive = undefined | null | boolean | number | string | bigint | symbol;

export type TypedArray =
    | Int8Array
    | Uint8Array
    | Uint8ClampedArray
    | Int16Array
    | Uint16Array
    | Int32Array
    | Uint32Array
    | Float32Array
    | Float64Array
    | BigInt64Array
    | BigUint64Array;

export type TypedArrayConstructor =
    | Int8ArrayConstructor
    | Uint8ArrayConstructor
    | Uint8ClampedArrayConstructor
    | Int16ArrayConstructor
    | Uint16ArrayConstructor
    | Int32ArrayConstructor
    | Uint32ArrayConstructor
    | Float32ArrayConstructor
    | Float64ArrayConstructor
    | BigInt64ArrayConstructor
    | BigUint64ArrayConstructor;

export type Builtin = Primitive | BuiltinObject;

export type BuiltinObject =
    | Object
    | Function
    | Boolean
    | Symbol
    | Error
    | EvalError
    | RangeError
    | ReferenceError
    | SyntaxError
    | TypeError
    | URIError
    | AggregateError
    | Number
    | BigInt
    | Math
    | Date
    | String
    | RegExp
    | Array<any>
    | Map<any, any>
    | Set<any>
    | WeakMap<any, any>
    | WeakSet<any>
    | Promise<any>
    | TypedArray
    | ArrayBuffer
    | SharedArrayBuffer
    | DataView
    | JSON
    | WebAssembly.Module
    | WebAssembly.Instance
    | WebAssembly.Memory
    | WebAssembly.Table
    | WebAssembly.Global
    | WebAssembly.CompileError
    | WebAssembly.LinkError
    | WebAssembly.RuntimeError;
