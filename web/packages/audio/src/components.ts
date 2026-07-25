/**
 * Barrel re-exporting the audio component classes.
 *
 * Each `@script` component now lives in its own file (one component per file,
 * mirroring the Unity / Cocos Creator authoring convention):
 * - `AudioListenerComponent` -> `./audio-listener-component`
 * - `AudioSourceComponent`   -> `./audio-source-component`
 *
 * This barrel keeps existing `from './components'` imports working.
 */
export type { AudioListenerComponentConfig } from './audio-listener-component';
export { AudioListenerComponent } from './audio-listener-component';
export type { AudioSourceComponentConfig } from './audio-source-component';
export { AudioSourceComponent } from './audio-source-component';
