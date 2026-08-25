const BLOCKED_KEYS: ReadonlySet<string> = new Set(['__proto__', 'prototype', 'constructor']);

export class BuilderError extends Error {
    public override readonly name: string = 'BuilderError';
    public readonly code: string;
    public readonly context?: unknown;

    constructor(message: string, code: string = 'ERR_BUILDER', context?: unknown) {
        super(message);
        this.code = code;
        this.context = context;
        Object.setPrototypeOf(this, new.target.prototype);
    }
}

export class MissingPropertyError extends BuilderError {
    public override readonly name: string = 'MissingPropertyError';
    public readonly missingKeys: readonly string[];

    constructor(missingKeys: readonly string[]) {
        super(
            `Cannot materialize instance. Missing required properties: [${missingKeys.join(', ')}]`,
            'ERR_MISSING_PROPERTIES',
            { missingKeys }
        );
        this.missingKeys = missingKeys;
    }
}

export class SchemaValidationError extends BuilderError {
    public override readonly name: string = 'SchemaValidationError';
    public readonly issues: readonly import('./types').ValidationIssue[];

    constructor(issues: readonly import('./types').ValidationIssue[]) {
        super(
            `Validation failure during build:\n${issues.map((i) => `  - [${i.path}] (${i.code}): ${i.message}`).join('\n')}`,
            'ERR_VALIDATION_FAILURE',
            { issues }
        );
        this.issues = issues;
    }
}

export class SecurityViolationError extends BuilderError {
    public override readonly name: string = 'SecurityViolationError';

    constructor(property: string) {
        super(`Access to prototype chain property "${property}" is blocked.`, 'ERR_SECURITY_VIOLATION', {
            property,
        });
    }
}

export function assertSafeKey(key: string): void {
    if (BLOCKED_KEYS.has(key)) {
        throw new SecurityViolationError(key);
    }
}
