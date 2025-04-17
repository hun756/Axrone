module.exports = {
    preset: 'ts-jest',
    testEnvironment: 'jsdom',
    setupFilesAfterEnv: ['<rootDir>/jest.setup.js'],
    testMatch: ['**/__tests__/**/*.spec.[jt]s?(x)', '**/?(*.)+(spec|test).[jt]s?(x)'],
    collectCoverageFrom: [
        'packages/*/src/**/*.{js,jsx,ts,tsx}',
        '!packages/*/src/**/*.d.ts',
        '!packages/*/src/**/__tests__/**',
        '!packages/*/src/**/__mocks__/**'
    ],
    coverageThreshold: {
        global: {
            branches: 80,
            functions: 80,
            lines: 80,
            statements: 80
        }
    },
    moduleNameMapper: {
        '^@axrone/(.*)$': '<rootDir>/packages/$1/src'
    }
};
