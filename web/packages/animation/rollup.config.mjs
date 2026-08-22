import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { createMultiEntryConfig } from '../../build/create-package-config.mjs';

const packageDir = path.dirname(fileURLToPath(import.meta.url));

export default createMultiEntryConfig({
    packageDir,
    entries: {
        index: 'src/index.ts',
        types: 'src/types.ts',
        clip: 'src/clip.ts',
        'blend-tree': 'src/blend-tree.ts',
        'blend-types': 'src/blend-types.ts',
        'blend-scratch': 'src/blend-scratch.ts',
        'blend-visitor': 'src/blend-visitor.ts',
        'blend-compile': 'src/blend-compile.ts',
        'blend-evaluate': 'src/blend-evaluate.ts',
        'blend-events': 'src/blend-events.ts',
        'blend-activities': 'src/blend-activities.ts',
        'blend-root-delta': 'src/blend-root-delta.ts',
        'blend-duration': 'src/blend-duration.ts',
        'pose-frame': 'src/pose-frame.ts',
        'pose-blend': 'src/pose-blend.ts',
        'state-machine': 'src/state-machine.ts',
        retargeting: 'src/retargeting.ts',
        ik: 'src/ik.ts',
        skinning: 'src/skinning.ts',
        controller: 'src/controller.ts',
        streaming: 'src/streaming.ts',
        'streaming-chunk': 'src/streaming-chunk.ts',
        pose: 'src/pose.ts',
        parameters: 'src/parameters.ts',
        rig: 'src/rig.ts',
        errors: 'src/errors.ts',
    },
});
