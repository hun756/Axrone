/**
 * R-03: Naming Convention Validation Test
 *
 * Enforces the NS_Category_Description_## naming convention for all scene entities,
 * prefab nodes, and UI nodes. This test acts as a "ratchet" — it documents current
 * violations but prevents new ones from being introduced.
 *
 * Convention (BIBLE/SOP-06):
 *   Format: NS_Category_Description_##
 *   Examples: EN_Enemy_Grunt_01, UI_Button_Play_01, FX_Explosion_Basic_01
 *
 * Valid namespace prefixes:
 *   EN = Enemy / Environment
 *   PL = Player
 *   FX = Effects
 *   UI = User Interface
 *   LM = Lighting
 *   CM = Camera
 *   AU = Audio
 *   AN = Animation
 *
 * AUDIT SUMMARY (as of initial baseline):
 *   Total entities/nodes audited: 66
 *   Total violations found: 63
 *   - Main.scene.json: 9 violations (GM_AssetStore_3D_Character matches pattern)
 *   - Cube.prefab: 1 violation (root entity)
 *   - Cube-copy.prefab: 1 violation (root entity)
 *   - GM_AssetStore_3D_Character.prefab: 26 violations (2 entities match pattern)
 *   - UI-test.ui.json: 26 violations (all UI nodes)
 *
 * RECOMMENDED RENAMES:
 *   Main.scene.json:
 *     "Main Camera"              -> "CM_Camera_Main_01"
 *     "Directional Light"        -> "LM_Light_Directional_01"
 *     "World"                    -> "EN_Environment_World_01"
 *     "Ground"                   -> "EN_Environment_Ground_01"
 *     "Player"                   -> "PL_Player_Default_01"
 *     "UI Host"                  -> "UI_Host_Main_01"
 *     "Actor"                    -> "PL_Player_Actor_01"
 *     "Actor 2"                  -> "PL_Player_Actor_02"
 *     "Cube"                     -> "EN_Prop_Cube_01"
 *
 *   Cube.prefab:
 *     "Cube" (scene + entity)    -> "EN_Prop_Cube_01"
 *
 *   Cube-copy.prefab:
 *     "Cube-copy" (scene + entity) -> "EN_Prop_CubeCopy_01"
 *
 *   GM_AssetStore_3D_Character.prefab:
 *     "GM_AssetStore_3D_Character" -> matches pattern but uses non-standard NS "GM"; consider "EN_Character_Hero_01"
 *     "Armature"                   -> "AN_Armature_Hero_01"
 *     "GM_AssetStore_3D_Main"     -> matches pattern but uses non-standard NS "GM"; consider "EN_Character_HeroMesh_01"
 *     "mixamorig:*" bones          -> "AN_Bone_<Name>_01" (25 bones)
 *
 *   UI-test.ui.json:
 *     "root"                       -> "UI_Root_Test_01"
 *     "Container"                  -> "UI_Container_Main_01"
 *     "widget-9"                   -> "UI_Button_Default_01"
 *     "widget-9-label"             -> "UI_Text_ButtonLabel_01"
 *     "widget-9-copy"              -> "UI_Button_Default_02"
 *     "widget-9-label-copy"        -> "UI_Text_ButtonLabel_02"
 *     "widget-9-copy-2"            -> "UI_Button_Default_03"
 *     "widget-9-label-copy-2"      -> "UI_Text_ButtonLabel_03"
 *     "widget-9-copy-3"            -> "UI_Button_Default_04"
 *     "widget-9-label-copy-3"      -> "UI_Text_ButtonLabel_04"
 *     "widget-9-copy-4"            -> "UI_Button_Default_05"
 *     "widget-9-label-copy-4"      -> "UI_Text_ButtonLabel_05"
 *     "widget-9-copy-5"            -> "UI_Button_Default_06"
 *     "widget-9-label-copy-5"      -> "UI_Text_ButtonLabel_06"
 *     "widget-10"                  -> "UI_Container_Controls_01"
 *     "widget-11"                  -> "UI_Slider_Default_01"
 *     "widget-11-track"            -> "UI_SliderTrack_Default_01"
 *     "widget-11-fill"             -> "UI_SliderFill_Default_01"
 *     "widget-11-handle"           -> "UI_SliderHandle_Default_01"
 *     "widget-12"                  -> "UI_Button_Stateful_01"
 *     "widget-12-label"            -> "UI_Text_ButtonLabel_07"
 *     "widget-13"                  -> "UI_Checkbox_Default_01"
 *     "widget-13-box"              -> "UI_CheckboxBox_Default_01"
 *     "widget-13-mark"             -> "UI_CheckboxMark_Default_01"
 *     "widget-13-label"            -> "UI_Text_CheckboxLabel_01"
 *     "widget-14"                  -> "UI_Image_Default_01"
 */

import { describe, expect, it } from 'vitest';
import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------

interface SceneEntity {
    readonly id: string;
    readonly name: string;
    readonly parentId: string | null;
    readonly enabled: boolean;
    readonly tag: string;
    readonly layer: string;
}

interface SceneDocument {
    readonly schemaVersion: number;
    readonly id: string;
    readonly name: string;
    readonly entities: readonly SceneEntity[];
}

interface UINode {
    readonly role: string;
    readonly key: string;
    readonly enabled: boolean;
    readonly children?: readonly UINode[];
}

interface UIDocument {
    readonly id: string;
    readonly name: string;
    readonly root: UINode;
}

interface NamingViolation {
    readonly filePath: string;
    readonly currentName: string;
    readonly expectedPattern: string;
}

// ---------------------------------------------------------------------------
// Constants
// ---------------------------------------------------------------------------

/**
 * Naming convention regex: NS_Category_Description_##
 * - NS: exactly 2 uppercase letters
 * - Category: one or more letters
 * - Description: alphanumeric + underscores
 * - Optional trailing _## (numeric suffix)
 */
const NAMING_PATTERN = /^[A-Z]{2}_[A-Za-z]+_[A-Za-z0-9_]+(_\d+)?$/;

const PROJECT_ROOT = resolve(__dirname, '..', '..', '..', '..', '..', '..');
const ASSETS_DIR = resolve(PROJECT_ROOT, 'Assets');

const SCENE_FILE = resolve(PROJECT_ROOT, 'Main.scene.json');
const PREFAB_CUBE = resolve(ASSETS_DIR, 'Prefab', 'Cube.prefab');
const PREFAB_CUBE_COPY = resolve(ASSETS_DIR, 'Prefab', 'Cube-copy.prefab');
const PREFAB_CHARACTER = resolve(ASSETS_DIR, 'Prefab', 'GM_AssetStore_3D_Character.prefab');
const UI_TEST = resolve(ASSETS_DIR, 'UI-test.ui.json');

/**
 * Known baseline violations. These are documented and tracked as TODO items.
 * The ratchet ensures no NEW violations are introduced beyond this set.
 */
const BASELINE_VIOLATIONS: ReadonlySet<string> = new Set([
    // Main.scene.json (9 violations — GM_AssetStore_3D_Character matches the pattern)
    'Main.scene.json::Main Camera',
    'Main.scene.json::Directional Light',
    'Main.scene.json::World',
    'Main.scene.json::Ground',
    'Main.scene.json::Player',
    'Main.scene.json::UI Host',
    'Main.scene.json::Actor',
    'Main.scene.json::Actor 2',
    'Main.scene.json::Cube',
    // Cube.prefab (1 violation)
    'Cube.prefab::Cube',
    // Cube-copy.prefab (1 violation)
    'Cube-copy.prefab::Cube-copy',
    // GM_AssetStore_3D_Character.prefab (26 violations — 2 entities match the pattern)
    'GM_AssetStore_3D_Character.prefab::Armature',
    'GM_AssetStore_3D_Character.prefab::mixamorig:Hips',
    'GM_AssetStore_3D_Character.prefab::mixamorig:Spine',
    'GM_AssetStore_3D_Character.prefab::mixamorig:Spine1',
    'GM_AssetStore_3D_Character.prefab::mixamorig:Spine2',
    'GM_AssetStore_3D_Character.prefab::mixamorig:Neck',
    'GM_AssetStore_3D_Character.prefab::mixamorig:Head',
    'GM_AssetStore_3D_Character.prefab::mixamorig:HeadTop_End',
    'GM_AssetStore_3D_Character.prefab::mixamorig:LeftShoulder',
    'GM_AssetStore_3D_Character.prefab::mixamorig:LeftArm',
    'GM_AssetStore_3D_Character.prefab::mixamorig:LeftForeArm',
    'GM_AssetStore_3D_Character.prefab::mixamorig:LeftHand',
    'GM_AssetStore_3D_Character.prefab::mixamorig:RightShoulder',
    'GM_AssetStore_3D_Character.prefab::mixamorig:RightArm',
    'GM_AssetStore_3D_Character.prefab::mixamorig:RightForeArm',
    'GM_AssetStore_3D_Character.prefab::mixamorig:RightHand',
    'GM_AssetStore_3D_Character.prefab::mixamorig:LeftUpLeg',
    'GM_AssetStore_3D_Character.prefab::mixamorig:LeftLeg',
    'GM_AssetStore_3D_Character.prefab::mixamorig:LeftFoot',
    'GM_AssetStore_3D_Character.prefab::mixamorig:LeftToeBase',
    'GM_AssetStore_3D_Character.prefab::mixamorig:LeftToe_End',
    'GM_AssetStore_3D_Character.prefab::mixamorig:RightUpLeg',
    'GM_AssetStore_3D_Character.prefab::mixamorig:RightLeg',
    'GM_AssetStore_3D_Character.prefab::mixamorig:RightFoot',
    'GM_AssetStore_3D_Character.prefab::mixamorig:RightToeBase',
    'GM_AssetStore_3D_Character.prefab::mixamorig:RightToe_End',
    // UI-test.ui.json (26 violations)
    'UI-test.ui.json::root',
    'UI-test.ui.json::Container',
    'UI-test.ui.json::widget-9',
    'UI-test.ui.json::widget-9-label',
    'UI-test.ui.json::widget-9-copy',
    'UI-test.ui.json::widget-9-label-copy',
    'UI-test.ui.json::widget-9-copy-2',
    'UI-test.ui.json::widget-9-label-copy-2',
    'UI-test.ui.json::widget-9-copy-3',
    'UI-test.ui.json::widget-9-label-copy-3',
    'UI-test.ui.json::widget-9-copy-4',
    'UI-test.ui.json::widget-9-label-copy-4',
    'UI-test.ui.json::widget-9-copy-5',
    'UI-test.ui.json::widget-9-label-copy-5',
    'UI-test.ui.json::widget-10',
    'UI-test.ui.json::widget-11',
    'UI-test.ui.json::widget-11-track',
    'UI-test.ui.json::widget-11-fill',
    'UI-test.ui.json::widget-11-handle',
    'UI-test.ui.json::widget-12',
    'UI-test.ui.json::widget-12-label',
    'UI-test.ui.json::widget-13',
    'UI-test.ui.json::widget-13-box',
    'UI-test.ui.json::widget-13-mark',
    'UI-test.ui.json::widget-13-label',
    'UI-test.ui.json::widget-14',
]);

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

function loadJsonFile<T>(filePath: string): T {
    const raw = readFileSync(filePath, 'utf-8');
    return JSON.parse(raw) as T;
}

function validateName(name: string): boolean {
    return NAMING_PATTERN.test(name);
}

function extractSceneEntityNames(sceneDoc: SceneDocument): string[] {
    return sceneDoc.entities.map((entity) => entity.name);
}

function extractUINodeKeys(node: UINode): string[] {
    const keys: string[] = [node.key];
    if (node.children) {
        for (const child of node.children) {
            keys.push(...extractUINodeKeys(child));
        }
    }
    return keys;
}

function collectViolations(
    filePath: string,
    names: readonly string[]
): NamingViolation[] {
    const violations: NamingViolation[] = [];
    for (const name of names) {
        if (!validateName(name)) {
            violations.push({
                filePath,
                currentName: name,
                expectedPattern: 'NS_Category_Description_##',
            });
        }
    }
    return violations;
}

function makeViolationKey(filePath: string, name: string): string {
    const fileName = filePath.replace(/\\/g, '/').split('/').pop() ?? filePath;
    return `${fileName}::${name}`;
}

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

describe('R-03: Naming Convention Validation', () => {
    describe('naming pattern regex', () => {
        it('accepts valid names matching NS_Category_Description_##', () => {
            expect(validateName('EN_Enemy_Grunt_01')).toBe(true);
            expect(validateName('UI_Button_Play_01')).toBe(true);
            expect(validateName('FX_Explosion_Basic_01')).toBe(true);
            expect(validateName('PL_Player_Default_01')).toBe(true);
            expect(validateName('LM_Light_Directional_01')).toBe(true);
            expect(validateName('CM_Camera_Main_01')).toBe(true);
            expect(validateName('AU_Audio_Ambient_01')).toBe(true);
            expect(validateName('AN_Animation_Idle_01')).toBe(true);
            expect(validateName('EN_Environment_World_01')).toBe(true);
        });

        it('accepts names without trailing numeric suffix', () => {
            expect(validateName('EN_Enemy_Grunt')).toBe(true);
            expect(validateName('UI_Button_Play')).toBe(true);
            expect(validateName('FX_Explosion_Basic')).toBe(true);
        });

        it('accepts names with multi-word descriptions using underscores', () => {
            expect(validateName('EN_Enemy_Dark_Knight_01')).toBe(true);
            expect(validateName('UI_Button_Main_Menu_01')).toBe(true);
            expect(validateName('FX_Explosion_Big_Bang_01')).toBe(true);
        });

        it('rejects names without namespace prefix', () => {
            expect(validateName('Cube')).toBe(false);
            expect(validateName('Player')).toBe(false);
            expect(validateName('Main Camera')).toBe(false);
        });

        it('rejects names with lowercase namespace prefix', () => {
            expect(validateName('en_Enemy_Grunt_01')).toBe(false);
            expect(validateName('ui_Button_Play_01')).toBe(false);
        });

        it('rejects names with single-letter namespace prefix', () => {
            expect(validateName('E_Enemy_Grunt_01')).toBe(false);
            expect(validateName('U_Button_Play_01')).toBe(false);
        });

        it('rejects names with three-letter namespace prefix', () => {
            expect(validateName('ENM_Enemy_Grunt_01')).toBe(false);
            expect(validateName('UID_Button_Play_01')).toBe(false);
        });

        it('rejects names with special characters like colons', () => {
            expect(validateName('mixamorig:Hips')).toBe(false);
            expect(validateName('EN_Bone:Spine_01')).toBe(false);
        });

        it('rejects names with hyphens', () => {
            expect(validateName('widget-9')).toBe(false);
            expect(validateName('UI_Button-Play_01')).toBe(false);
            expect(validateName('Cube-copy')).toBe(false);
        });

        it('rejects names with spaces', () => {
            expect(validateName('Main Camera')).toBe(false);
            expect(validateName('Directional Light')).toBe(false);
            expect(validateName('UI Host')).toBe(false);
        });

        it('rejects names with numbers in the first segment (not 2 uppercase letters)', () => {
            // "3D_Model_Hero_01" starts with "3D" which is digit+letter, not 2 uppercase letters
            expect(validateName('3D_Model_Hero_01')).toBe(false);
            // "1A_Test_Name_01" starts with digit, not 2 uppercase letters
            expect(validateName('1A_Test_Name_01')).toBe(false);
        });

        it('matches GM_AssetStore_3D_Character format (valid pattern but non-standard namespace)', () => {
            // GM is a valid 2-letter uppercase prefix per the regex, so this matches the pattern.
            // However, GM is not in the approved namespace list (EN, PL, FX, UI, LM, CM, AU, AN).
            // Namespace validation is a separate semantic check beyond the regex.
            expect(validateName('GM_AssetStore_3D_Character')).toBe(true);
        });
    });

    describe('Main.scene.json entity names', () => {
        const sceneDoc = loadJsonFile<SceneDocument>(SCENE_FILE);
        const entityNames = extractSceneEntityNames(sceneDoc);
        const violations = collectViolations(SCENE_FILE, entityNames);

        it('loads the scene file and finds all entities', () => {
            expect(entityNames.length).toBe(10);
        });

        it('documents all current violations as baseline', () => {
            expect(violations.length).toBe(9);
            for (const violation of violations) {
                const key = makeViolationKey(violation.filePath, violation.currentName);
                expect(BASELINE_VIOLATIONS.has(key)).toBe(true);
            }
        });

        it('does not introduce NEW violations beyond the baseline', () => {
            const newViolations = violations.filter((v) => {
                const key = makeViolationKey(v.filePath, v.currentName);
                return !BASELINE_VIOLATIONS.has(key);
            });
            expect(newViolations.length).toBe(0);
        });

        it.todo('rename "Main Camera" to "CM_Camera_Main_01"');
        it.todo('rename "Directional Light" to "LM_Light_Directional_01"');
        it.todo('rename "World" to "EN_Environment_World_01"');
        it.todo('rename "Ground" to "EN_Environment_Ground_01"');
        it.todo('rename "Player" to "PL_Player_Default_01"');
        it.todo('rename "UI Host" to "UI_Host_Main_01"');
        it.todo('rename "Actor" to "PL_Player_Actor_01"');
        it.todo('rename "Actor 2" to "PL_Player_Actor_02"');
        it.todo('rename "Cube" to "EN_Prop_Cube_01"');
    });

    describe('Cube.prefab entity names', () => {
        const prefabDoc = loadJsonFile<SceneDocument>(PREFAB_CUBE);
        const entityNames = extractSceneEntityNames(prefabDoc);
        const violations = collectViolations(PREFAB_CUBE, entityNames);

        it('loads the prefab and finds all entities', () => {
            expect(entityNames.length).toBe(1);
        });

        it('documents all current violations as baseline', () => {
            expect(violations.length).toBe(1);
            for (const violation of violations) {
                const key = makeViolationKey(violation.filePath, violation.currentName);
                expect(BASELINE_VIOLATIONS.has(key)).toBe(true);
            }
        });

        it('does not introduce NEW violations beyond the baseline', () => {
            const newViolations = violations.filter((v) => {
                const key = makeViolationKey(v.filePath, v.currentName);
                return !BASELINE_VIOLATIONS.has(key);
            });
            expect(newViolations.length).toBe(0);
        });

        it.todo('rename "Cube" to "EN_Prop_Cube_01" in Cube.prefab');
    });

    describe('Cube-copy.prefab entity names', () => {
        const prefabDoc = loadJsonFile<SceneDocument>(PREFAB_CUBE_COPY);
        const entityNames = extractSceneEntityNames(prefabDoc);
        const violations = collectViolations(PREFAB_CUBE_COPY, entityNames);

        it('loads the prefab and finds all entities', () => {
            expect(entityNames.length).toBe(1);
        });

        it('documents all current violations as baseline', () => {
            expect(violations.length).toBe(1);
            for (const violation of violations) {
                const key = makeViolationKey(violation.filePath, violation.currentName);
                expect(BASELINE_VIOLATIONS.has(key)).toBe(true);
            }
        });

        it('does not introduce NEW violations beyond the baseline', () => {
            const newViolations = violations.filter((v) => {
                const key = makeViolationKey(v.filePath, v.currentName);
                return !BASELINE_VIOLATIONS.has(key);
            });
            expect(newViolations.length).toBe(0);
        });

        it.todo('rename "Cube-copy" to "EN_Prop_CubeCopy_01" in Cube-copy.prefab');
    });

    describe('GM_AssetStore_3D_Character.prefab entity names', () => {
        const prefabDoc = loadJsonFile<SceneDocument>(PREFAB_CHARACTER);
        const entityNames = extractSceneEntityNames(prefabDoc);
        const violations = collectViolations(PREFAB_CHARACTER, entityNames);

        it('loads the prefab and finds all entities', () => {
            expect(entityNames.length).toBe(28);
        });

        it('documents all current violations as baseline', () => {
            expect(violations.length).toBe(26);
            for (const violation of violations) {
                const key = makeViolationKey(violation.filePath, violation.currentName);
                expect(BASELINE_VIOLATIONS.has(key)).toBe(true);
            }
        });

        it('does not introduce NEW violations beyond the baseline', () => {
            const newViolations = violations.filter((v) => {
                const key = makeViolationKey(v.filePath, v.currentName);
                return !BASELINE_VIOLATIONS.has(key);
            });
            expect(newViolations.length).toBe(0);
        });

        it.todo('consider renaming "GM_AssetStore_3D_Character" to use standard NS (e.g. "EN_Character_Hero_01")');
        it.todo('rename "Armature" to "AN_Armature_Hero_01"');
        it.todo('consider renaming "GM_AssetStore_3D_Main" to use standard NS (e.g. "EN_Character_HeroMesh_01")');
        it.todo('rename all "mixamorig:*" bones to "AN_Bone_<Name>_01" format (25 bones)');
    });

    describe('UI-test.ui.json node names', () => {
        const uiDoc = loadJsonFile<UIDocument>(UI_TEST);
        const nodeKeys = extractUINodeKeys(uiDoc.root);
        const violations = collectViolations(UI_TEST, nodeKeys);

        it('loads the UI file and finds all node keys', () => {
            expect(nodeKeys.length).toBe(26);
        });

        it('documents all current violations as baseline', () => {
            expect(violations.length).toBe(26);
            for (const violation of violations) {
                const key = makeViolationKey(violation.filePath, violation.currentName);
                expect(BASELINE_VIOLATIONS.has(key)).toBe(true);
            }
        });

        it('does not introduce NEW violations beyond the baseline', () => {
            const newViolations = violations.filter((v) => {
                const key = makeViolationKey(v.filePath, v.currentName);
                return !BASELINE_VIOLATIONS.has(key);
            });
            expect(newViolations.length).toBe(0);
        });

        it.todo('rename "root" to "UI_Root_Test_01"');
        it.todo('rename "Container" to "UI_Container_Main_01"');
        it.todo('rename all "widget-*" keys to "UI_<Role>_<Description>_##" format (24 widgets)');
    });

    describe('cross-file baseline integrity', () => {
        it('ensures baseline violation count matches expected total', () => {
            expect(BASELINE_VIOLATIONS.size).toBe(63);
        });

        it('ensures all baseline violations are still present in source files', () => {
            const allCurrentViolations: string[] = [];

            const mainScene = loadJsonFile<SceneDocument>(SCENE_FILE);
            for (const name of extractSceneEntityNames(mainScene)) {
                if (!validateName(name)) {
                    allCurrentViolations.push(makeViolationKey(SCENE_FILE, name));
                }
            }

            const cubePrefab = loadJsonFile<SceneDocument>(PREFAB_CUBE);
            for (const name of extractSceneEntityNames(cubePrefab)) {
                if (!validateName(name)) {
                    allCurrentViolations.push(makeViolationKey(PREFAB_CUBE, name));
                }
            }

            const cubeCopyPrefab = loadJsonFile<SceneDocument>(PREFAB_CUBE_COPY);
            for (const name of extractSceneEntityNames(cubeCopyPrefab)) {
                if (!validateName(name)) {
                    allCurrentViolations.push(makeViolationKey(PREFAB_CUBE_COPY, name));
                }
            }

            const characterPrefab = loadJsonFile<SceneDocument>(PREFAB_CHARACTER);
            for (const name of extractSceneEntityNames(characterPrefab)) {
                if (!validateName(name)) {
                    allCurrentViolations.push(makeViolationKey(PREFAB_CHARACTER, name));
                }
            }

            const uiDoc = loadJsonFile<UIDocument>(UI_TEST);
            for (const key of extractUINodeKeys(uiDoc.root)) {
                if (!validateName(key)) {
                    allCurrentViolations.push(makeViolationKey(UI_TEST, key));
                }
            }

            // Every current violation must be in the baseline
            for (const key of allCurrentViolations) {
                expect(BASELINE_VIOLATIONS.has(key), `Unexpected violation: ${key}`).toBe(true);
            }

            // Baseline must not contain violations that no longer exist
            for (const baselineKey of BASELINE_VIOLATIONS) {
                expect(allCurrentViolations, `Missing baseline violation: ${baselineKey}`).toContain(baselineKey);
            }
        });

        it('ensures total audited entity/node count is at least 64', () => {
            const mainScene = loadJsonFile<SceneDocument>(SCENE_FILE);
            const cubePrefab = loadJsonFile<SceneDocument>(PREFAB_CUBE);
            const cubeCopyPrefab = loadJsonFile<SceneDocument>(PREFAB_CUBE_COPY);
            const characterPrefab = loadJsonFile<SceneDocument>(PREFAB_CHARACTER);
            const uiDoc = loadJsonFile<UIDocument>(UI_TEST);

            const totalAudited =
                extractSceneEntityNames(mainScene).length +
                extractSceneEntityNames(cubePrefab).length +
                extractSceneEntityNames(cubeCopyPrefab).length +
                extractSceneEntityNames(characterPrefab).length +
                extractUINodeKeys(uiDoc.root).length;

            expect(totalAudited).toBeGreaterThanOrEqual(66);
        });
    });
});
