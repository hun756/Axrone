import eslint from '@eslint/js';
import tsPlugin from '@typescript-eslint/eslint-plugin';
import tsParser from '@typescript-eslint/parser';
import vitest from 'eslint-plugin-vitest';
import globals from 'globals';

const envGlobals = {
    ...globals.node,
    ...globals.browser,
};

export default [
    eslint.configs.recommended,
    {
        files: ['src/**/*.ts'],
        ignores: ['**/__tests__/**', 'dist/**'],
        languageOptions: {
            parser: tsParser,
            parserOptions: {
                ecmaVersion: 2022,
                sourceType: 'module',
            },
            globals: envGlobals,
        },
        plugins: {
            '@typescript-eslint': tsPlugin,
        },
        rules: {
            'no-unused-vars': 'off',
            '@typescript-eslint/no-unused-vars': ['error', { argsIgnorePattern: '^_' }],
            '@typescript-eslint/explicit-module-boundary-types': 'error',
            '@typescript-eslint/no-explicit-any': 'warn',
            'no-console': ['warn', { allow: ['warn', 'error'] }],
            '@typescript-eslint/no-namespace': 'off',
        },
    },
    {
        files: ['src/**/__tests__/**'],
        languageOptions: {
            parser: tsParser,
            parserOptions: {
                ecmaVersion: 2022,
                sourceType: 'module',
            },
            globals: {
                ...envGlobals,
                ...vitest.configs.recommended.languageOptions?.globals,
            },
        },
        plugins: {
            '@typescript-eslint': tsPlugin,
            vitest,
        },
        rules: {
            ...vitest.configs.recommended.rules,
            'no-unused-vars': 'off',
            '@typescript-eslint/no-unused-vars': ['error', { argsIgnorePattern: '^_' }],
            'no-console': 'off',
            '@typescript-eslint/explicit-module-boundary-types': 'off',
            '@typescript-eslint/no-namespace': 'off',
        },
    },
];
