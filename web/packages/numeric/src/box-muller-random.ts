export class DefaultRandomGenerator {
    private static _instance: DefaultRandomGenerator | null = null;

    private constructor() {}

    static getInstance(): DefaultRandomGenerator {
        if (!DefaultRandomGenerator._instance) {
            DefaultRandomGenerator._instance = new DefaultRandomGenerator();
        }
        return DefaultRandomGenerator._instance;
    }

    float(): number {
        return Math.random();
    }
    floatBetween(min: number, max: number): number {
        return min + Math.random() * (max - min);
    }
    int(min: number, max: number): number {
        return Math.floor(min + Math.random() * (max - min + 1));
    }
}
