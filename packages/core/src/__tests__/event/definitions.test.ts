import {
    EventCallback,
    UnsubscribeFn,
    EventKey,
    EventMap,
    EventPriority,
    isValidEventName,
    isValidCallback,
    isValidPriority,
    PRIORITY_VALUES,
    DEFAULT_PRIORITY,
    ExtractEventData,
    EventNames,
} from '../../event/event';

interface TestUserEvents {
    'user:login': { userId: string; timestamp: number };
    'user:logout': { userId: string; reason?: string };
}

interface TestSystemEvents {
    'system:error': { error: Error; context: string };
    'system:startup': { version: string; environment: string };
}

describe('EventEmitter: Type Definitions', () => {
    describe('isValidEventName', () => {
        it('Must recognize valid event names correctly', () => {
            expect(isValidEventName('user:login')).toBe(true);
            expect(isValidEventName('system:error')).toBe(true);
            expect(isValidEventName('custom-event')).toBe(true);
            expect(isValidEventName('a')).toBe(true);
            expect(isValidEventName('test123')).toBe(true);
            expect(isValidEventName('event_with_underscore')).toBe(true);
        });

        it('Should reject invalid event names', () => {
            expect(isValidEventName('')).toBe(false);
            expect(isValidEventName(null)).toBe(false);
            expect(isValidEventName(undefined)).toBe(false);
            expect(isValidEventName(123)).toBe(false);
            expect(isValidEventName({})).toBe(false);
            expect(isValidEventName([])).toBe(false);
            expect(isValidEventName(true)).toBe(false);
        });

        it('Handle edge cases correctly', () => {
            expect(isValidEventName(' ')).toBe(true);
            expect(isValidEventName('🎉')).toBe(true);
            expect(isValidEventName('你好')).toBe(true);
        });
    });

    describe('isValidCallback', () => {
        it('Must correctly recognize valid callback functions', () => {
            const syncCallback = (data: any) => {};
            const asyncCallback = async (data: any) => {};
            const arrowFunction = (data: any) => console.log(data);
            const namedFunction = function handler(data: any) {};

            expect(isValidCallback(syncCallback)).toBe(true);
            expect(isValidCallback(asyncCallback)).toBe(true);
            expect(isValidCallback(arrowFunction)).toBe(true);
            expect(isValidCallback(namedFunction)).toBe(true);
            expect(isValidCallback(() => {})).toBe(true);
            expect(isValidCallback(function () {})).toBe(true);
        });

        it('Reject invalid callback values', () => {
            expect(isValidCallback(null)).toBe(false);
            expect(isValidCallback(undefined)).toBe(false);
            expect(isValidCallback('function')).toBe(false);
            expect(isValidCallback(123)).toBe(false);
            expect(isValidCallback({})).toBe(false);
            expect(isValidCallback([])).toBe(false);
            expect(isValidCallback(true)).toBe(false);
        });

        it('Must recognize built-in functions', () => {
            expect(isValidCallback(console.log)).toBe(true);
            expect(isValidCallback(JSON.parse)).toBe(true);
            expect(isValidCallback(Math.max)).toBe(true);
        });
    });
});
