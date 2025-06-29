export default {
    preset: 'ts-jest/presets/js-with-ts-esm',
    testEnvironment: 'jsdom',
    setupFilesAfterEnv: ['./jest.setup.js'],
    testMatch: ['**/__tests__/**/*.spec.[jt]s?(x)', '**/?(*.)+(spec|test).[jt]s?(x)'],
    extensionsToTreatAsEsm: ['.ts', '.tsx', '.mts'],
    moduleNameMapper: {
        '^@axrone/(.*)$': '<rootDir>/packages/$1/src',
        'punycode': '<rootDir>/node_modules/punycode2'
    },
    transform: {
        '^.+\\.(ts|tsx|mts)$': [
            'ts-jest',
            {
                useESM: true,
                isolatedModules: true,
                sourceMap: true
            }
        ]
    }
};