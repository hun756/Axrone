// Vitest setup for Node.js-like unit tests
import { vi, expect } from 'vitest';

// Global test utilities (cast global to any to allow assignment)
(global as any).expect = expect;
(global as any).vi = vi;

// Math utilities mock (sadece gerekirse)
Object.assign(global, {
  performance: {
    now: () => Date.now(),
  },
});

// Console düzenlemesi
const originalLog = console.log;
console.log = (...args: any[]) => {
  if (process.env.NODE_ENV === 'test') {
    // Test sırasında daha temiz output
    return;
  }
  originalLog(...args);
};
