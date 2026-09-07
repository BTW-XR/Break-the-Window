import { useEffect, useRef, useState } from "react";
import * as THREE from "three";
import { OrbitControls } from "three/examples/jsm/controls/OrbitControls.js";
import type { LayerState, ReplayFrame, SampleRecord } from "../types";

interface ReplayViewportProps {
  replayFrame: ReplayFrame | null;
  layers: LayerState;
  trailLengthLimit: number | null;
}

interface OverlayLabel {
  key: string;
  text: string;
  left: number;
  top: number;
}

const HEAD_COLOR = "#475569";
const LEFT_HAND_COLOR = "#15803d";
const RIGHT_HAND_COLOR = "#2563eb";
const UNITY_TO_THREE_HANDEDNESS = new THREE.Matrix4().makeScale(
  -1,
  1,
  1,
);

function makeQuaternion(rotation: {
  x: number;
  y: number;
  z: number;
  w: number;
}): THREE.Quaternion {
  const source = new THREE.Quaternion(
    rotation.x,
    rotation.y,
    rotation.z,
    rotation.w,
  );
  const rotationMatrix =
    new THREE.Matrix4().makeRotationFromQuaternion(source);
  rotationMatrix.premultiply(UNITY_TO_THREE_HANDEDNESS);
  rotationMatrix.multiply(UNITY_TO_THREE_HANDEDNESS);
  return new THREE.Quaternion().setFromRotationMatrix(rotationMatrix);
}

function makeVector(position: {
  x: number;
  y: number;
  z: number;
}): THREE.Vector3 {
  return new THREE.Vector3(-position.x, position.y, position.z);
}

function mulberry32(seed: number): () => number {
  let state = seed | 0;
  return () => {
    state |= 0;
    state = (state + 0x6d2b79f5) | 0;
    let t = Math.imul(state ^ (state >>> 15), 1 | state);
    t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t;
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
}

function fnv1a(value: string): number {
  let hash = 2166136261;
  for (let index = 0; index < value.length; index += 1) {
    hash ^= value.charCodeAt(index);
    hash = Math.imul(hash, 16777619);
  }
  return hash >>> 0;
}

function getColor(
  sessionId: string,
  objectId: string,
  hueStart: number,
  hueRange: number,
  satMin: number = 0.75,
  satMax: number = 0.75,
  lightMin: number = 0.5,
  lightMax: number = 0.5,
): THREE.Color {
  const rng = mulberry32(fnv1a(`${sessionId}:${objectId}`));
  const hue = hueStart + (2 * rng() - 0.5) * hueRange;
  const sat = rng() * (satMax - satMin) + satMin;
  const lightness = rng() * (lightMax - lightMin) + lightMin;
  return new THREE.Color().setHSL(hue / 360, sat, lightness);
}

function getModuleColor(
  sessionId: string,
  objectId: string,
): THREE.Color {
  return getColor(sessionId, objectId, 25, 0);
}

function getWebsiteColor(
  sessionId: string,
  objectId: string,
): THREE.Color {
  return getColor(sessionId, objectId, 215, 0);
}

export default function ReplayViewport(
  props: ReplayViewportProps,
): JSX.Element {
  const rootRef = useRef<HTMLDivElement | null>(null);
  const sceneRef = useRef<THREE.Scene | null>(null);
  const cameraRef = useRef<THREE.PerspectiveCamera | null>(null);
  const rendererRef = useRef<THREE.WebGLRenderer | null>(null);
  const dynamicGroupRef = useRef<THREE.Group | null>(null);
  const controlsRef = useRef<OrbitControls | null>(null);
  const labelAnchorsRef = useRef<
    Array<{ key: string; text: string; world: THREE.Vector3 }>
  >([]);
  const [overlayLabels, setOverlayLabels] = useState<OverlayLabel[]>(
    [],
  );
  const labelLayersRef = useRef({
    showModuleLabels: props.layers.showModuleLabels,
    showWebsiteLabels: props.layers.showWebsiteLabels,
  });
  labelLayersRef.current = {
    showModuleLabels: props.layers.showModuleLabels,
    showWebsiteLabels: props.layers.showWebsiteLabels,
  };
  const axesGroupRef = useRef<THREE.Group | null>(null);

  useEffect(() => {
    if (!rootRef.current) {
      return;
    }

    const scene = new THREE.Scene();
    scene.background = new THREE.Color("#ffffff");
    scene.fog = new THREE.Fog("#ffffff", 5, 12);

    const camera = new THREE.PerspectiveCamera(
      45,
      rootRef.current.clientWidth /
        Math.max(rootRef.current.clientHeight, 1),
      0.01,
      100,
    );
    camera.position.set(0, 1.5, -3);
    camera.lookAt(0, 1.1, 0);

    const renderer = new THREE.WebGLRenderer({ antialias: true });
    renderer.setPixelRatio(window.devicePixelRatio);
    renderer.setSize(
      rootRef.current.clientWidth,
      rootRef.current.clientHeight,
    );
    rootRef.current.appendChild(renderer.domElement);

    const controls = new OrbitControls(camera, renderer.domElement);
    controls.enableDamping = true;
    controls.dampingFactor = 0.08;
    controls.target.set(0, 1.1, 0);
    controls.minDistance = 0.2;
    controls.maxDistance = 20;
    controls.update();

    const ambient = new THREE.AmbientLight("#ffffff", 1.1);
    const directional = new THREE.DirectionalLight("#fff8eb", 1.15);
    directional.position.set(2, 4, 3);
    const grid = new THREE.GridHelper(8, 16, "#c8d0da", "#e4e7eb");
    const axesMat = new THREE.MeshBasicMaterial({
      vertexColors: false,
    });
    const axisLen = 0.5;
    const axisRadius = 0.005;
    const makeAxis = (dir: THREE.Vector3, color: number) => {
      const mesh = new THREE.Mesh(
        new THREE.CylinderGeometry(
          axisRadius,
          axisRadius,
          axisLen,
          6,
        ),
        new THREE.MeshBasicMaterial({ color }),
      );
      mesh.position.copy(dir.clone().multiplyScalar(axisLen / 2));
      mesh.quaternion.setFromUnitVectors(
        new THREE.Vector3(0, 1, 0),
        dir,
      );
      return mesh;
    };
    const axesX = makeAxis(new THREE.Vector3(-1, 0, 0), 0xff0000);
    const axesY = makeAxis(new THREE.Vector3(0, 1, 0), 0x00ff00);
    const axesZ = makeAxis(new THREE.Vector3(0, 0, 1), 0x0000ff);
    const axesGroup = new THREE.Group();
    axesGroup.add(axesX, axesY, axesZ);
    scene.add(ambient, directional, grid, axesGroup);
    axesGroupRef.current = axesGroup;

    const dynamicGroup = new THREE.Group();
    scene.add(dynamicGroup);

    let frameId = 0;
    const renderFrame = () => {
      controls.update();
      renderer.render(scene, camera);
      updateOverlayLabels();
      frameId = window.requestAnimationFrame(renderFrame);
    };

    const updateOverlayLabels = () => {
      if (
        (!labelLayersRef.current.showModuleLabels &&
          !labelLayersRef.current.showWebsiteLabels) ||
        !rootRef.current
      ) {
        setOverlayLabels((current) =>
          current.length === 0 ? current : [],
        );
        return;
      }

      setOverlayLabels(
        labelAnchorsRef.current.map((label) => {
          const screen = labelProjection(
            label.world,
            camera,
            renderer.domElement,
          );
          return {
            key: label.key,
            text: label.text,
            left: screen.left,
            top: screen.top,
          };
        }),
      );
    };

    const resizeObserver = new ResizeObserver(() => {
      if (!rootRef.current) {
        return;
      }

      camera.aspect =
        rootRef.current.clientWidth /
        Math.max(rootRef.current.clientHeight, 1);
      camera.updateProjectionMatrix();
      renderer.setSize(
        rootRef.current.clientWidth,
        rootRef.current.clientHeight,
      );
      updateOverlayLabels();
    });
    resizeObserver.observe(rootRef.current);

    frameId = window.requestAnimationFrame(renderFrame);

    sceneRef.current = scene;
    cameraRef.current = camera;
    rendererRef.current = renderer;
    dynamicGroupRef.current = dynamicGroup;
    controlsRef.current = controls;

    return () => {
      window.cancelAnimationFrame(frameId);
      resizeObserver.disconnect();
      controls.dispose();
      renderer.dispose();
      rootRef.current?.removeChild(renderer.domElement);
      sceneRef.current = null;
      cameraRef.current = null;
      rendererRef.current = null;
      dynamicGroupRef.current = null;
      controlsRef.current = null;
      labelAnchorsRef.current = [];
      axesGroupRef.current = null;
    };
  }, []);

  useEffect(() => {
    const scene = sceneRef.current;
    const camera = cameraRef.current;
    const renderer = rendererRef.current;
    const dynamicGroup = dynamicGroupRef.current;
    if (!scene || !camera || !renderer || !dynamicGroup) {
      return;
    }

    while (dynamicGroup.children.length > 0) {
      const child = dynamicGroup.children[0];
      dynamicGroup.remove(child);
      const geometryHolder = child as THREE.Object3D & {
        geometry?: THREE.BufferGeometry;
      };
      if (geometryHolder.geometry) {
        geometryHolder.geometry.dispose();
      }
      const materialHolder = child as THREE.Object3D & {
        material?: THREE.Material | THREE.Material[];
      };
      if (materialHolder.material) {
        const material = materialHolder.material;
        if (Array.isArray(material)) {
          material.forEach((entry) => entry.dispose());
        } else {
          material.dispose();
        }
      }
    }

    const labelAnchors: Array<{
      key: string;
      text: string;
      world: THREE.Vector3;
    }> = [];
    const frame = props.replayFrame;
    const sample = frame?.sample ?? null;
    if (!sample) {
      labelAnchorsRef.current = [];
      setOverlayLabels([]);
      return;
    }

    renderPose(dynamicGroup, sample, props.layers);
    renderModules(dynamicGroup, sample, labelAnchors, props.layers);
    renderWebsites(dynamicGroup, sample, labelAnchors, props.layers);
    if (props.layers.showTrails && frame?.session) {
      renderTrails(
        dynamicGroup,
        frame.session.samples,
        sample.elapsed_seconds,
        props.layers,
        props.trailLengthLimit,
      );
    }

    labelAnchorsRef.current = labelAnchors;
    if (
      !props.layers.showModuleLabels &&
      !props.layers.showWebsiteLabels
    ) {
      setOverlayLabels([]);
    }
  }, [props.replayFrame, props.layers]);

  useEffect(() => {
    if (axesGroupRef.current) {
      axesGroupRef.current.visible = props.layers.showAxes;
    }
  }, [props.layers.showAxes]);

  return (
    <div className="replay-surface" ref={rootRef}>
      {props.replayFrame?.sample ? null : (
        <div className="empty-state">No sample frame available.</div>
      )}
      {overlayLabels.map((label) => (
        <div
          key={label.key}
          className="scene-label"
          style={{ left: label.left, top: label.top }}
        >
          {label.text}
        </div>
      ))}
    </div>
  );
}

function renderPose(
  group: THREE.Group,
  sample: SampleRecord,
  layers: LayerState,
) {
  if (layers.showHead) {
    const head = createPoseMarker(
      makeVector(sample.head.position),
      HEAD_COLOR,
      0.06,
      makeQuaternion(sample.head.rotation),
      true,
    );
    group.add(head);
  }

  if (layers.showLeftHand && sample.left_hand.tracked) {
    const left = createPoseMarker(
      makeVector(sample.left_hand.position),
      LEFT_HAND_COLOR,
      0.05,
      makeQuaternion(sample.left_hand.rotation),
      false,
    );
    group.add(left);
  }

  if (layers.showRightHand && sample.right_hand.tracked) {
    const right = createPoseMarker(
      makeVector(sample.right_hand.position),
      RIGHT_HAND_COLOR,
      0.05,
      makeQuaternion(sample.right_hand.rotation),
      false,
    );
    group.add(right);
  }
}

function createPoseMarker(
  position: THREE.Vector3,
  color: string,
  scale: number,
  quaternion: THREE.Quaternion | null,
  showLookArrow: boolean,
): THREE.Object3D {
  const marker = new THREE.Group();
  marker.position.copy(position);
  if (quaternion) {
    marker.quaternion.copy(quaternion);
  }

  const sphere = new THREE.Mesh(
    new THREE.SphereGeometry(scale, 18, 18),
    new THREE.MeshStandardMaterial({ color }),
  );
  marker.add(sphere);

  if (showLookArrow) {
    const arrowGroup = new THREE.Group();

    const shaft = new THREE.Mesh(
      new THREE.CylinderGeometry(
        scale * 0.09,
        scale * 0.09,
        scale * 1.6,
        10,
      ),
      new THREE.MeshStandardMaterial({ color }),
    );
    shaft.rotation.x = Math.PI / 2;
    shaft.position.z = scale * 1.1;

    const head = new THREE.Mesh(
      new THREE.ConeGeometry(scale * 0.24, scale * 0.7, 12),
      new THREE.MeshStandardMaterial({ color }),
    );
    head.rotation.x = Math.PI / 2;
    head.position.z = scale * 2.05;

    arrowGroup.add(shaft, head);
    marker.add(arrowGroup);
  } else {
    const forward = new THREE.Mesh(
      new THREE.ConeGeometry(scale * 0.35, scale * 1.2, 12),
      new THREE.MeshStandardMaterial({ color }),
    );
    forward.rotation.x = Math.PI / 2;
    forward.position.z = scale * 0.7;
    marker.add(forward);
  }

  return marker;
}

function renderModules(
  group: THREE.Group,
  sample: SampleRecord,
  labelAnchors: Array<{
    key: string;
    text: string;
    world: THREE.Vector3;
  }>,
  layers: LayerState,
) {
  if (!layers.showModules) {
    return;
  }

  for (const module of sample.modules) {
    const color = getModuleColor(sample.session_id, module.object_id);
    const material = new THREE.MeshBasicMaterial({
      color,
      side: THREE.DoubleSide,
    });
    const mesh = new THREE.Mesh(
      new THREE.PlaneGeometry(1, 1),
      material,
    );
    mesh.position.copy(makeVector(module.realPosition));
    mesh.scale.set(
      Math.max(module.realScale.x, 0.01) + 0.04,
      Math.max(module.realScale.y, 0.01) + 0.04,
      1,
    );
    mesh.quaternion.copy(makeQuaternion(module.realRotation));
    mesh.userData.objectId = module.object_id;
    group.add(mesh);

    if (layers.showModuleLabels) {
      labelAnchors.push({
        key: module.object_id,
        text: module.object_id,
        world: new THREE.Vector3(
          mesh.position.x,
          mesh.position.y + Math.max(module.realScale.y, 0.01) * 0.65,
          mesh.position.z,
        ),
      });
    }
  }
}

function renderWebsites(
  group: THREE.Group,
  sample: SampleRecord,
  labelAnchors: Array<{
    key: string;
    text: string;
    world: THREE.Vector3;
  }>,
  layers: LayerState,
) {
  if (!layers.showWebsites) {
    return;
  }

  for (const website of sample.websites) {
    const color = getWebsiteColor(
      sample.session_id,
      website.object_id,
    );

    const baseWidth = Math.max(website.scale.x, 0.01);
    const baseHeight = Math.max(website.scale.y, 0.01);
    const inset = 0.004;

    const borderGeo = new THREE.PlaneGeometry(1, 1);
    const borderMat = new THREE.MeshBasicMaterial({
      color: "#000000",
      side: THREE.DoubleSide,
      transparent: true,
      opacity: 0.9,
    });
    const borderMesh = new THREE.Mesh(borderGeo, borderMat);
    borderMesh.position.copy(makeVector(website.position));
    const localOffset = new THREE.Vector3(0, 0, 0.001);
    localOffset.applyQuaternion(makeQuaternion(website.rotation));
    borderMesh.position.add(localOffset);
    borderMesh.scale.set(baseWidth, baseHeight, 1);
    borderMesh.quaternion.copy(makeQuaternion(website.rotation));
    group.add(borderMesh);

    const material = new THREE.MeshBasicMaterial({
      color,
      side: THREE.DoubleSide,
    });
    const geometry = new THREE.PlaneGeometry(1, 1);
    const mesh = new THREE.Mesh(geometry, material);
    mesh.position.copy(makeVector(website.position));
    mesh.scale.set(baseWidth - inset * 2, baseHeight - inset * 2, 1);
    mesh.quaternion.copy(makeQuaternion(website.rotation));
    mesh.userData.objectId = website.object_id;
    group.add(mesh);

    if (layers.showWebsiteLabels) {
      labelAnchors.push({
        key: website.object_id,
        text: `${website.object_id} • ${website.module_object_id}`,
        world: new THREE.Vector3(
          mesh.position.x,
          mesh.position.y + mesh.scale.y * 0.65,
          mesh.position.z,
        ),
      });
    }
  }
}

function renderTrails(
  group: THREE.Group,
  samples: SampleRecord[],
  currentElapsedSeconds: number,
  layers: LayerState,
  trailLengthLimit: number | null,
) {
  const visibleTrailPoints = samples.filter(
    (sample) => sample.elapsed_seconds <= currentElapsedSeconds,
  );
  const trailPoints =
    trailLengthLimit === null
      ? visibleTrailPoints
      : visibleTrailPoints.slice(-trailLengthLimit);

  if (layers.showHead) {
    addTrail(
      group,
      trailPoints.map((sample) => makeVector(sample.head.position)),
      HEAD_COLOR,
    );
  }
  if (layers.showLeftHand) {
    addTrail(
      group,
      trailPoints
        .filter((sample) => sample.left_hand.tracked)
        .map((sample) => makeVector(sample.left_hand.position)),
      LEFT_HAND_COLOR,
    );
  }
  if (layers.showRightHand) {
    addTrail(
      group,
      trailPoints
        .filter((sample) => sample.right_hand.tracked)
        .map((sample) => makeVector(sample.right_hand.position)),
      RIGHT_HAND_COLOR,
    );
  }
}

function addTrail(
  group: THREE.Group,
  points: THREE.Vector3[],
  color: string,
) {
  if (points.length < 2) {
    return;
  }

  const geometry = new THREE.BufferGeometry().setFromPoints(points);
  const material = new THREE.LineBasicMaterial({ color });
  group.add(new THREE.Line(geometry, material));
}

function labelProjection(
  world: THREE.Vector3,
  camera: THREE.Camera,
  canvas: HTMLCanvasElement,
) {
  const point = world.clone();
  point.project(camera);
  return {
    left: ((point.x + 1) / 2) * canvas.clientWidth,
    top: ((-point.y + 1) / 2) * canvas.clientHeight,
  };
}
