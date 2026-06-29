export interface StackFrameCapture {
  readonly function: string;
  readonly lineNumber?: number;
  readonly columnNumber?: number;
}

export class StackCaptureEngine {
  private static cache = new Map<string, StackFrameCapture[]>();
  private static maxCacheSize = 2048;
  private static readonly MAX_FRAME_LENGTH = 128;

  captureStack(depth: number = 50): StackFrameCapture[] {
    const stackString = this.generateStackTrace(depth);
    return this.parseStackFrames(stackString, depth);
  }

  captureStackSignature(): string {
    const frames = this.captureStack();
    return this.computeHash(frames);
  }

  private generateStackTrace(maxDepth: number): string {
    try {
      const error = new Error();
      if (typeof Error.captureStackTrace === 'function') {
        Error.captureStackTrace(error, this.constructor as unknown as Function);
        return error.stack ?? '';
      }
      return error.stack ?? '';
    } catch {
      return '';
    }
  }

  private parseStackFrames(stackString: string, maxDepth: number): StackFrameCapture[] {
    const frames: StackFrameCapture[] = [];
    for (const line of stackString.split('\n')) {
      if (!line || (line.startsWith('Error') && !line.includes('/'))) continue;
      const trimmed = line.trim();
      const match = trimmed.match(/at\s+(?:(.+?)\s+\((.+?):(\d+):(\d+)\)|(.+?):(\d+):(\d+))/);
      if (match) {
        if (match[1]) {
          frames.push({
            function: match[1],
            lineNumber: parseInt(match[3], 10) || undefined,
            columnNumber: parseInt(match[4], 10) || undefined,
          });
        } else {
          frames.push({
            function: match[5] || 'unknown',
            lineNumber: parseInt(match[6], 10) || undefined,
            columnNumber: parseInt(match[7], 10) || undefined,
          });
        }
      } else if (trimmed.startsWith('at ')) {
        frames.push({ function: trimmed.slice(3).trim() });
      }

      if (frames.length >= maxDepth) break;
    }
    return frames;
  }

  private computeHash(frames: StackFrameCapture[]): string {
    let hash = 0x811c9dc5n;
    for (const frame of frames) {
      const functionStr = frame.function.length > StackCaptureEngine.MAX_FRAME_LENGTH
        ? frame.function.slice(0, StackCaptureEngine.MAX_FRAME_LENGTH)
        : frame.function;
      for (let i = 0; i < functionStr.length; i++) {
        hash ^= BigInt(functionStr.charCodeAt(i)) << (BigInt(i % 4) * 3n);
      }
    }
    return (hash & 0xffffffffffffffffn).toString(16);
  }

  static clearCache(): void {
    StackCaptureEngine.cache.clear();
  }
}
