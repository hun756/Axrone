export class FlameGraphNode implements AsyncDisposable {
  readonly name: string;
  private durationNs = 0n;
  private uniqueSignatures = new Set<string>();
  private disposed = false;

  constructor(name: string) {
    this.name = name;
  }

  addDuration(durationNs: bigint): void {
    if (this.disposed) return;
    this.durationNs += durationNs;
  }

  addSignature(signature: string): void {
    if (!this.disposed) {
      this.uniqueSignatures.add(signature);
    }
  }

  getUniqueSignatures(): string[] {
    if (!this.disposed) return [...this.uniqueSignatures];
    return [];
  }

  getDurationNs(): bigint {
    return this.durationNs;
  }

  [Symbol.asyncDispose](): Promise<void> {
    this.disposed = true;
    return Promise.resolve();
  }
}

export class FlameGraphBuilder implements AsyncDisposable {
  private readonly rootNodes: Map<string, FlameGraphNode> = new Map();
  private disposed = false;

  recordFrame(functionName: string, durationNs?: bigint): void {
    if (this.disposed) return;
    let node = this.rootNodes.get(functionName);
    if (!node) {
      node = new FlameGraphNode(functionName);
      this.rootNodes.set(functionName, node);
    }
    if (durationNs !== undefined) node.addDuration(durationNs);
  }

  recordSignature(functionName: string, signature?: string): void {
    if (this.disposed || !signature) return;
    let node = this.rootNodes.get(functionName);
    if (!node) {
      node = new FlameGraphNode(functionName);
      this.rootNodes.set(functionName, node);
    }
    node.addSignature(signature);
  }

  getRoots(): readonly FlameGraphNode[] {
    return [...this.rootNodes.values()];
  }

  [Symbol.asyncDispose](): Promise<void> {
    for (const node of this.rootNodes.values()) {
      node[Symbol.asyncDispose]();
    }
    this.disposed = true;
    return Promise.resolve();
  }
}
