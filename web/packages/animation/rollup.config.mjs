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
