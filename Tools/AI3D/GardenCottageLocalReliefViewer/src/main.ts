import * as THREE from 'three';
import { OrbitControls } from 'three/examples/jsm/controls/OrbitControls.js';
import { GLTFExporter } from 'three/examples/jsm/exporters/GLTFExporter.js';
import { createLocalReliefDiorama } from './localReliefDiorama';

const mount = document.querySelector<HTMLDivElement>('#app')!;
const scene = new THREE.Scene();
scene.background = new THREE.Color(0xe7cfb7);

const camera = new THREE.PerspectiveCamera(31, innerWidth / innerHeight, 0.02, 100);
// Keep the review close to frontal while retaining enough parallax to expose
// the shallow physical depth.  The previous 32-degree side view overemphasised
// the silhouette walls and was a poor semantic-texture review angle.
const initialCamera = new THREE.Vector3(0.4, 2.55, 11.8);
camera.position.copy(initialCamera);

const renderer = new THREE.WebGLRenderer({ antialias: true, alpha: false });
renderer.setPixelRatio(Math.min(devicePixelRatio, 2));
renderer.setSize(innerWidth, innerHeight);
renderer.outputColorSpace = THREE.SRGBColorSpace;
renderer.toneMapping = THREE.ACESFilmicToneMapping;
renderer.toneMappingExposure = 1.02;
renderer.shadowMap.enabled = true;
renderer.shadowMap.type = THREE.PCFSoftShadowMap;
mount.appendChild(renderer.domElement);

const controls = new OrbitControls(camera, renderer.domElement);
controls.target.set(0, 1.62, 0);
controls.enableDamping = true;
controls.minDistance = 3;
controls.maxDistance = 18;

scene.add(new THREE.HemisphereLight(0xffead6, 0x7b695e, 1.8));
const key = new THREE.DirectionalLight(0xffdfc2, 4.1);
key.position.set(-4, 7, 5);
key.castShadow = true;
key.shadow.mapSize.set(2048, 2048);
scene.add(key);
const rim = new THREE.DirectionalLight(0xa9d8ff, 1.3);
rim.position.set(5, 4, -4);
scene.add(rim);

const ground = new THREE.Mesh(
  new THREE.PlaneGeometry(30, 30),
  new THREE.ShadowMaterial({ color: 0x684a3b, opacity: 0.18 }),
);
ground.rotation.x = -Math.PI / 2;
ground.position.y = -0.015;
ground.receiveShadow = true;
scene.add(ground);

const reliefRoot = await createLocalReliefDiorama();
scene.add(reliefRoot);

document.querySelector('#reset')!.addEventListener('click', () => {
  camera.position.copy(initialCamera);
  controls.target.set(0, 1.62, 0);
  controls.update();
});

document.querySelector('#export')!.addEventListener('click', async () => {
  const exporter = new GLTFExporter();
  const result = await exporter.parseAsync(reliefRoot, { binary: true });
  const blob = new Blob([result as ArrayBuffer], { type: 'model/gltf-binary' });
  const link = document.createElement('a');
  link.href = URL.createObjectURL(blob);
  link.download = 'GardenCottage_V76_LocalRelief_QA_Group.glb';
  link.click();
  URL.revokeObjectURL(link.href);
});

function frame(): void {
  controls.update();
  renderer.render(scene, camera);
  requestAnimationFrame(frame);
}
frame();

addEventListener('resize', () => {
  camera.aspect = innerWidth / innerHeight;
  camera.updateProjectionMatrix();
  renderer.setSize(innerWidth, innerHeight);
});
