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

    describe('isValidPriority', () => {
        it('must correctly recognize valid priority values', () => {
            expect(isValidPriority('high')).toBe(true);
            expect(isValidPriority('normal')).toBe(true);
            expect(isValidPriority('low')).toBe(true);
        });

        it('reject invalid priority values', () => {
            expect(isValidPriority('urgent')).toBe(false);
            expect(isValidPriority('medium')).toBe(false);
            expect(isValidPriority('highest')).toBe(false);
            expect(isValidPriority('lowest')).toBe(false);
            expect(isValidPriority('')).toBe(false);
            expect(isValidPriority(null)).toBe(false);
            expect(isValidPriority(undefined)).toBe(false);
            expect(isValidPriority(1)).toBe(false);
            expect(isValidPriority(['high'])).toBe(false);
            expect(isValidPriority({ priority: 'high' })).toBe(false);
        });

        it('should test case sensitivity', () => {
            expect(isValidPriority('HIGH')).toBe(false);
            expect(isValidPriority('High')).toBe(false);
            expect(isValidPriority('NORMAL')).toBe(false);
            expect(isValidPriority('Low')).toBe(false);
        });
    });
});

// Constants Tests
describe('Constants', () => {
    describe('PRIORITY_VALUES', () => {
        it('Must contain correct priority values', () => {
            expect(PRIORITY_VALUES.high).toBe(0);
            expect(PRIORITY_VALUES.normal).toBe(1);
            expect(PRIORITY_VALUES.low).toBe(2);
        });

        it('Priority order must be correct', () => {
            expect(PRIORITY_VALUES.high).toBeLessThan(PRIORITY_VALUES.normal);
            expect(PRIORITY_VALUES.normal).toBeLessThan(PRIORITY_VALUES.low);
        });

        it('All priority values ​​must be unique', () => {
            const values = Object.values(PRIORITY_VALUES);
            const uniqueValues = [...new Set(values)];
            expect(values.length).toBe(uniqueValues.length);
        });

        it('Must be numeric values', () => {
            Object.values(PRIORITY_VALUES).forEach((value) => {
                expect(typeof value).toBe('number');
                expect(Number.isInteger(value)).toBe(true);
                expect(value).toBeGreaterThanOrEqual(0);
            });
        });
    });

    describe('DEFAULT_PRIORITY', () => {
        it('Must be normal priority', () => {
            expect(DEFAULT_PRIORITY).toBe('normal');
        });

        it('Must be valid priority', () => {
            expect(isValidPriority(DEFAULT_PRIORITY)).toBe(true);
        });

        it('Must be in PRIORITY_VALUES', () => {
            expect(DEFAULT_PRIORITY in PRIORITY_VALUES).toBe(true);
        });
    });
});
