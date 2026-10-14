import * as THREE from 'three';
import { GLTFLoader } from 'three/examples/jsm/loaders/GLTFLoader.js';

const loader = new GLTFLoader();

function normalizeHeight(object: THREE.Object3D, height: number): void {
  const bounds = new THREE.Box3().setFromObject(object);
  const size = bounds.getSize(new THREE.Vector3());
  const scale = height / Math.max(size.y, 1e-6);
  object.scale.setScalar(scale);
  const scaled = new THREE.Box3().setFromObject(object);
  const centre = scaled.getCenter(new THREE.Vector3());
  object.position.sub(centre);
  object.position.y += scaled.getSize(new THREE.Vector3()).y * 0.5;
}

function tuneClayMaterials(object: THREE.Object3D): void {
  object.traverse((node) => {
    const mesh = node as THREE.Mesh;
    if (!mesh.isMesh) return;
    mesh.castShadow = true;
    mesh.receiveShadow = true;
    const materials = Array.isArray(mesh.material) ? mesh.material : [mesh.material];
    for (const material of materials) {
      const standard = material as THREE.MeshStandardMaterial;
      if (!standard.isMeshStandardMaterial) continue;
      standard.roughness = Math.max(standard.roughness ?? 0, 0.78);
      standard.metalness = Math.min(standard.metalness ?? 0, 0.04);
      standard.alphaTest = Math.max(standard.alphaTest ?? 0, 0.45);
      standard.needsUpdate = true;
    }
  });
}

export async function createLocalReliefDiorama(): Promise<THREE.Group> {
  const root = new THREE.Group();
  root.name = 'GardenCottage_V76_LocalRelief_QA';

  const [vineGltf, compoundGltf] = await Promise.all([
    loader.loadAsync('./models/part06_local_relief.glb'),
    loader.loadAsync('./models/part07_local_relief.glb'),
  ]);

  const vine = vineGltf.scene;
  vine.name = 'Part_06_CurvedFloweringVine_LocalRelief';
  normalizeHeight(vine, 3.55);
  tuneClayMaterials(vine);
  vine.position.x -= 1.85;
  vine.rotation.y = THREE.MathUtils.degToRad(4);

  const compound = compoundGltf.scene;
  compound.name = 'Part_07_SignFenceFlowerbed_LocalRelief';
  normalizeHeight(compound, 2.85);
  tuneClayMaterials(compound);
  compound.position.x += 1.45;
  compound.rotation.y = THREE.MathUtils.degToRad(-4);

  root.add(vine, compound);
  root.userData.sculptRuntime = {
    nodes: { vine, compound },
    sockets: {
      vineFacade: { owner: vine.name, normal: [0, 0, -1] },
      compoundFoundation: { owner: compound.name, normal: [0, 1, 0] },
    },
    colliders: [
      { owner: vine.name, kind: 'indexed-relief-triangles' },
      { owner: compound.name, kind: 'indexed-relief-triangles' },
    ],
    destructionGroups: [],
  };
  return root;
}
