import { describe, it, expect } from 'vitest';
import {
  ClockError,
  ClockNotRunningError,
  ClockAlreadyRunningError,
  ClockOverflowError,
  ClockSkewError,
  ClockResolutionError,
  isClockError,
} from '../clock-error';

describe('ClockError', () => {
  it('should create a basic clock error', () => {
    const error = new ClockError('clock failure');
    expect(error.name).toBe('ClockError');
    expect(error.message).toBe('clock failure');
  });

  it('should create ClockNotRunningError', () => {
    const error = new ClockNotRunningError();
    expect(error.name).toBe('ClockNotRunningError');
    expect(error.message).toBe('Clock is not running');
    expect(isClockError(error)).toBe(true);
  });

  it('should create ClockAlreadyRunningError', () => {
    const error = new ClockAlreadyRunningError();
    expect(error.name).toBe('ClockAlreadyRunningError');
    expect(error.message).toBe('Clock is already running');
  });

  it('should create ClockOverflowError', () => {
    const error = new ClockOverflowError();
    expect(error.message).toBe('Clock value overflow');
  });

  it('should create ClockSkewError', () => {
    const error = new ClockSkewError();
    expect(error.message).toBe('Clock skew detected');
  });

  it('should create ClockResolutionError', () => {
    const error = new ClockResolutionError();
    expect(error.name).toBe('ClockResolutionError');
  });
});
