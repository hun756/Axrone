import { StackIntegrityError } from './errors';
import type { NodeId, StackCapacity, StackSize } from './types';

const CAPACITY_MASK = 0x7fffffff;
const SIZE_MASK = 0x7fffffff;
const NODE_ID_MASK = 0xffffffff;

/** @stable */
export const createStackCapacity = (value: number): StackCapacity => {
    const masked = value & CAPACITY_MASK;
    if (masked !== value || value <= 0) {
        throw new StackIntegrityError('Invalid capacity value', { value, masked });
    }
    return masked as StackCapacity;
};

/** @stable */
export const createStackSize = (value: number): StackSize => {
    const masked = value & SIZE_MASK;
    if (masked !== value || value < 0) {
        throw new StackIntegrityError('Invalid size value', { value, masked });
    }
    return masked as StackSize;
};

/** @stable */
export const createNodeId = (): NodeId => {
    return ((Math.random() * NODE_ID_MASK) | 0) as NodeId;
};
