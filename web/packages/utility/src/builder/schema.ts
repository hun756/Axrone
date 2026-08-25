import type { ValidationIssue, ValidationResult } from './types';
import { assertSafeKey } from './errors';

export class Schema<T extends object> {
    private readonly fieldValidators: Map<keyof T, Array<(val: unknown, root: T) => ValidationIssue | null>> =
        new Map();
    private readonly rootValidators: Array<(root: T) => ValidationIssue | null> = [];

    public field<K extends keyof T>(
        key: K,
        predicate: (value: T[K], root: T) => boolean,
        message: string,
        code: string = 'ERR_FIELD_RULE'
    ): this {
        assertSafeKey(String(key));
        let rules = this.fieldValidators.get(key);
        if (rules === undefined) {
            rules = [];
            this.fieldValidators.set(key, rules);
        }
        rules.push((val, root) => {
            if (!predicate(val as T[K], root)) {
                return { path: String(key), message, code, received: val };
            }
            return null;
        });
        return this;
    }

    public refine(
        predicate: (root: T) => boolean,
        message: string,
        path: string = '$',
        code: string = 'ERR_REFINEMENT_RULE'
    ): this {
        this.rootValidators.push((root) => {
            if (!predicate(root)) {
                return { path, message, code, received: root };
            }
            return null;
        });
        return this;
    }

    public validate(target: T): ValidationResult<T> {
        const issues: ValidationIssue[] = [];

        for (const [key, rules] of this.fieldValidators) {
            const val = target[key];
            for (let i = 0; i < rules.length; i++) {
                const issue = rules[i]!(val, target);
                if (issue !== null) {
                    issues.push(issue);
                }
            }
        }

        for (let i = 0; i < this.rootValidators.length; i++) {
            const issue = this.rootValidators[i]!(target);
            if (issue !== null) {
                issues.push(issue);
            }
        }

        if (issues.length > 0) {
            return { ok: false, issues };
        }

        return { ok: true, value: target };
    }
}
