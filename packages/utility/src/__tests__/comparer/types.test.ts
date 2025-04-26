import {
    PropertyPath,
    ExtractPropertyType,
    DeepPartial,
    KeysOfType,
    ComparerOptions,
    EqualityComparerOptions,
} from '@axrone/utility';

describe('TypeScript Type Definitions', () => {
    describe('PropertyPath Type', () => {
        interface Person {
            id: number;
            name: string;
            address: {
                street: string;
                city: string;
                zipCode: string;
            };
            contacts: Array<{
                type: string;
                value: string;
            }>;
        }

        test('PropertyPath should work with direct properties', () => {
            const path1: PropertyPath<Person> = 'id';
            const path2: PropertyPath<Person> = 'name';
            const path3: PropertyPath<Person> = 'address';

            // @ts-expect-error
            const invalid1: PropertyPath<Person> = 'nonExistent';

            // @ts-expect-error
            const invalid2: PropertyPath<Person> = 0;

            expect(true).toBe(true);
        });

        describe('DeepPartial Type', () => {
            interface DeepObject {
                prop1: string;
                prop2: number;
                nested: {
                    inner1: boolean;
                    inner2: string;
                    deeplyNested: {
                        deepProp: number;
                    };
                };
                optionalProp?: string;
                arrayProp: Array<{
                    id: number;
                    name: string;
                }>;
            }

            test('DeepPartial should make all properties optional', () => {
                type PartialDeepObject = DeepPartial<DeepObject>;

                const empty: PartialDeepObject = {};

                const partial1: PartialDeepObject = {
                    prop1: 'test',
                };

                const partial2: PartialDeepObject = {
                    nested: {
                        inner1: true,
                    },
                };

                const partial3: PartialDeepObject = {
                    nested: {
                        deeplyNested: {
                        },
                    },
                };

                const partial4: PartialDeepObject = {
                    arrayProp: [
                        {
                            id: 1,
                        },
                    ],
                };

                const full: PartialDeepObject = {
                    prop1: 'test',
                    prop2: 42,
                    nested: {
                        inner1: true,
                        inner2: 'inner',
                        deeplyNested: {
                            deepProp: 100,
                        },
                    },
                    optionalProp: 'optional',
                    arrayProp: [
                        { id: 1, name: 'one' },
                        { id: 2, name: 'two' },
                    ],
                };

                expect(true).toBe(true);
            });
        });
    });
});
