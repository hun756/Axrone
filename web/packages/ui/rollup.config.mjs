import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { createMultiEntryConfig } from '../../build/create-package-config.mjs';

const packageDir = path.dirname(fileURLToPath(import.meta.url));

export default createMultiEntryConfig({
    packageDir,
    entries: {
        index: 'src/index.ts',
        'types/index': 'src/types.ts',
        'errors/index': 'src/errors.ts',
        'runtime/index': 'src/runtime.ts',
        'layout/index': 'src/layout.ts',
        'render/index': 'src/render.ts',
        'text/index': 'src/text.ts',
        'font/index': 'src/font.ts',
        'widget/index': 'src/widget.ts',
        'controls/index': 'src/controls.ts',
    },
});
