export type EventCallback<T> = (data: T) => void | Promise<void>;
export type UnsubscribeFn = () => boolean;
export type EventKey<T> = string & keyof T;
export type EventMap = Record<string, any>;
export type EventPriority = 'high' | 'normal' | 'low';

export type ExtractEventData<
    TEventMap extends EventMap,
    TEventKey extends keyof TEventMap,
> = TEventMap[TEventKey];

export type EventNames<T extends EventMap> = keyof T & string;

export type OptionalData<T> = T extends undefined ? T | void : T;

export function isValidEventName(eventName: unknown): eventName is string {
    return typeof eventName === 'string' && eventName.length > 0;
}

export function isValidCallback(callback: unknown): callback is EventCallback<any> {
    return typeof callback === 'function';
}

export function isValidPriority(priority: unknown): priority is EventPriority {
    return typeof priority === 'string' && ['high', 'normal', 'low'].includes(priority);
}

export const PRIORITY_VALUES: Record<EventPriority, number> = {
    high: 0,
    normal: 1,
    low: 2,
} as const;

export const DEFAULT_PRIORITY: EventPriority = 'normal';

export interface EventOptions {
    readonly captureRejections?: boolean;
    readonly maxListeners?: number;
    readonly weakReferences?: boolean;
    readonly immediateDispatch?: boolean;
    readonly concurrencyLimit?: number;
    readonly bufferSize?: number;
    readonly gcIntervalMs?: number;
}

export const DEFAULT_OPTIONS: Required<EventOptions> = Object.freeze({
    captureRejections: false,
    maxListeners: 10,
    weakReferences: false,
    immediateDispatch: true,
    concurrencyLimit: Infinity,
    bufferSize: 1000,
    gcIntervalMs: 60000,
} as const);

export const MEMORY_USAGE_SYMBOLS = Object.freeze({
    staticSubscriptions: Symbol('staticSubscriptions'),
    subscriptionMaps: Symbol('subscriptionMaps'),
    priorityQueues: Symbol('priorityQueues'),
    eventBuffer: Symbol('eventBuffer'),
} as const);

export abstract class BaseError extends Error {
    override readonly name: string;

    constructor(name: string, message: string, options?: ErrorOptions) {
        super(message, options);
        this.name = name;
        Object.setPrototypeOf(this, new.target.prototype);
        (Error as any).captureStackTrace?.(this, this.constructor);
    }
}

export class EventError extends BaseError {
    constructor(message: string, options?: ErrorOptions) {
        super('EventError', message, options);
    }
}

export class EventNotFoundError extends EventError {
    readonly eventName: string;

    constructor(eventName: string) {
        super(`Event "${eventName}" not found`);
        this.eventName = eventName;
    }
}

export class EventQueueFullError extends EventError {
    readonly eventName: string;

    constructor(eventName: string, bufferSize: number) {
        super(`Event queue for "${eventName}" is full (${bufferSize} items)`);
        this.eventName = eventName;
    }
}

export class EventHandlerError extends EventError {
    readonly originalError: unknown;
    readonly eventName: string;

    constructor(eventName: string, originalError: unknown) {
        const message =
            originalError instanceof Error ? originalError.message : String(originalError);
        super(`Handler error for "${eventName}": ${message}`);
        this.eventName = eventName;
        this.originalError = originalError;

        if (originalError instanceof Error && originalError.stack) {
            this.stack = `${this.stack}\nCaused by: ${originalError.stack}`;
        }
    }
}

export interface Subscription<T = unknown> {
    readonly id: symbol;
    readonly event: string;
    readonly callback: EventCallback<T>;
    readonly once: boolean;
    readonly priority: EventPriority;
    readonly createdAt: number;
    lastExecuted?: number;
    executionCount: number;
}

export interface SubscriptionOptions {
    readonly once?: boolean;
    readonly priority?: EventPriority;
}

export interface EventMetrics {
    readonly emit: {
        readonly count: number;
        readonly timing: {
            readonly avg: number;
            readonly max: number;
            readonly min: number;
            readonly total: number;
        };
    };
    readonly execution: {
        readonly count: number;
        readonly errors: number;
        readonly timing: {
            readonly avg: number;
            readonly max: number;
            readonly min: number;
            readonly total: number;
        };
    };
}

export interface QueuedEvent<T = any> {
    readonly id: number;
    readonly event: string;
    readonly data: T;
    readonly timestamp: number;
    readonly priority: EventPriority;
}

