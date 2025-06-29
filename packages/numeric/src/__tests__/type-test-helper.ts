export const createValidVec2Objects = () => [
    { x: 0, y: 0 },
    { x: 1, y: 2 },
    { x: -3.5, y: 4.2 },
    { x: 0, y: 0, z: 1 },
    Object.assign(Object.create(null), { x: 1, y: 2 }),
    Object.create({ x: 1, y: 2 }),
    {
        _x: 1,
        _y: 2,
        get x() {
            return this._x;
        },
        get y() {
            return this._y;
        },
    },
];

export const createInvalidVec2Objects = () => [
    {},
    { x: 0 },
    { y: 0 },
    { x: '0', y: 0 },
    { x: 0, y: '0' },
    { x: null, y: 0 },
    { x: 0, y: undefined },
    { x: BigInt(1), y: 2 },
    null,
    undefined,
    0,
    'string',
    true,
    Symbol(),
    () => ({ x: 1, y: 2 }),
];

export const createValidVec2Tuples = () => [
    [0, 0],
    [1, 2],
    [-3.5, 4.2],
    Object.freeze([1, 2]),
    (function () {
        const arr : number[] = [];
        arr[0] = 1;
        arr[1] = 2;
        return arr;
    })(), 
];

export const createInvalidVec2Tuples = () => [
    [],
    [0],
    [0, 0, 0],
    ['0', 0],
    [0, '0'],
    [null, 0],
    [0, undefined],
    [BigInt(1), 2],
    { 0: 1, 1: 2, length: 2 },
    new Float32Array([1, 2]),
    null,
    undefined,
    0,
    'string',
    true,
    Symbol(),
    { x: 0, y: 0 },
];
