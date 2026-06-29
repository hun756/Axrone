export interface StackFrameCapture {
  readonly function: string;
  readonly file?: string;
  readonly lineNumber?: number;
  readonly columnNumber?: number;
}

type StackFormat = 'v8' | 'spidermonkey' | 'javascriptcore' | 'unknown';

export class StackCaptureEngine {
  private static cache = new Map<string, StackFrameCapture[]>();
  private static maxCacheSize = 4096;
  private static readonly MAX_FRAME_LENGTH = 128;
  private static readonly V8_REGEX = /at\s+(?:(.+?)\s+\((.+?):(\d+):(\d+)\)|(.+?):(\d+):(\d+))/;
  private static readonly SPIDERMONKEY_REGEX = /(.+?)@(.+?):(\d+):(\d+)/;
  private static readonly JSC_REGEX = /(.+?)@(.+?):(\d+):(\d+)/;
  private static readonly JSC_ARROW_REGEX = /(.+?)\s*->\s*(.+?):(\d+):(\d+)/;

  private format: StackFormat = 'unknown';

  captureStack(depth: number = 50): StackFrameCapture[] {
    const stackString = this.generateStackTrace(depth);
    const cacheKey = stackString.slice(0, 512);
    const cached = StackCaptureEngine.cache.get(cacheKey);
    if (cached) return cached;
    const frames = this.parseStackFrames(stackString, depth);
    if (frames.length > 0) {
      this.addToCache(cacheKey, frames);
    }
    return frames;
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

  private detectFormat(line: string): StackFormat {
    if (StackCaptureEngine.V8_REGEX.test(line)) return 'v8';
    if (StackCaptureEngine.SPIDERMONKEY_REGEX.test(line)) return 'spidermonkey';
    if (StackCaptureEngine.JSC_ARROW_REGEX.test(line)) return 'javascriptcore';
    if (StackCaptureEngine.JSC_REGEX.test(line)) return 'javascriptcore';
    return 'unknown';
  }

  private parseStackFrames(stackString: string, maxDepth: number): StackFrameCapture[] {
    const lines = stackString.split('\n');
    const frames: StackFrameCapture[] = [];

    for (const rawLine of lines) {
      const line = rawLine.trim();
      if (!line) continue;
      if (line.startsWith('Error') && !line.includes('/')) continue;

      if (this.format === 'unknown' && line.startsWith('at ')) {
        this.format = 'v8';
      } else if (this.format === 'unknown') {
        this.format = this.detectFormat(line);
      }

      let frame: StackFrameCapture | null = null;

      if (this.format === 'v8') {
        frame = this.parseV8Line(line);
      } else if (this.format === 'spidermonkey') {
        frame = this.parseSpiderMonkeyLine(line);
      } else if (this.format === 'javascriptcore') {
        frame = this.parseJavaScriptCoreLine(line);
      }

      if (frame) {
        frames.push(frame);
        if (frames.length >= maxDepth) break;
      }
    }
    return frames;
  }

  private parseV8Line(line: string): StackFrameCapture | null {
    const match = line.match(StackCaptureEngine.V8_REGEX);
    if (!match) {
      if (line.startsWith('at ')) {
        return { function: line.slice(3).trim() };
      }
      return null;
    }
    if (match[1]) {
      return {
        function: match[1],
        file: match[2],
        lineNumber: parseInt(match[3], 10) || undefined,
        columnNumber: parseInt(match[4], 10) || undefined,
      };
    }
    return {
      function: match[5] || 'unknown',
      file: match[6],
      lineNumber: parseInt(match[6], 10) || undefined,
      columnNumber: parseInt(match[7], 10) || undefined,
    };
  }

  private parseSpiderMonkeyLine(line: string): StackFrameCapture | null {
    const match = line.match(StackCaptureEngine.SPIDERMONKEY_REGEX);
    if (!match) return null;
    return {
      function: match[1],
      file: match[2],
      lineNumber: parseInt(match[3], 10) || undefined,
      columnNumber: parseInt(match[4], 10) || undefined,
    };
  }

  private parseJavaScriptCoreLine(line: string): StackFrameCapture | null {
    const arrowMatch = line.match(StackCaptureEngine.JSC_ARROW_REGEX);
    if (arrowMatch) {
      return {
        function: arrowMatch[1],
        file: arrowMatch[2],
        lineNumber: parseInt(arrowMatch[3], 10) || undefined,
        columnNumber: parseInt(arrowMatch[4], 10) || undefined,
      };
    }
    const match = line.match(StackCaptureEngine.JSC_REGEX);
    if (!match) return null;
    return {
      function: match[1],
      file: match[2],
      lineNumber: parseInt(match[3], 10) || undefined,
      columnNumber: parseInt(match[4], 10) || undefined,
    };
  }

  private addToCache(key: string, frames: StackFrameCapture[]): void {
    if (StackCaptureEngine.cache.size >= StackCaptureEngine.maxCacheSize) {
      const firstKey = StackCaptureEngine.cache.keys().next().value;
      if (firstKey !== undefined) {
        StackCaptureEngine.cache.delete(firstKey);
      }
    }
    StackCaptureEngine.cache.set(key, frames);
  }

  private computeHash(frames: StackFrameCapture[]): string {
    let hash = 0x811c9dc5n;
    for (const frame of frames) {
      const fn = frame.function.length > StackCaptureEngine.MAX_FRAME_LENGTH
        ? frame.function.slice(0, StackCaptureEngine.MAX_FRAME_LENGTH)
        : frame.function;
      for (let i = 0; i < fn.length; i++) {
        hash ^= BigInt(fn.charCodeAt(i)) << (BigInt(i % 4) * 3n);
      }
    }
    return (hash & 0xffffffffffffffffn).toString(16);
  }

  getCacheSize(): number {
    return StackCaptureEngine.cache.size;
  }

  static clearCache(): void {
    StackCaptureEngine.cache.clear();
  }

  static setMaxCacheSize(size: number): void {
    StackCaptureEngine.maxCacheSize = size;
  }
}
