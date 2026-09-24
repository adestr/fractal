const controlsPositionKey = "fractal.mandelbrot.controlsPosition";

export function initControlsPanel(panel: HTMLElement, handle: HTMLElement) {
  if (!panel || !handle || panel.dataset.dragBound === "true") {
    return;
  }

  panel.dataset.dragBound = "true";
  restoreControlsPosition(panel);

  let pointerId: number | null = null;
  let offsetX = 0;
  let offsetY = 0;

  const onResize = () => {
    if (!panel.isConnected) {
      window.removeEventListener("resize", onResize);
      return;
    }

    if (!panel.style.left) {
      return;
    }

    placePanel(panel, parseFloat(panel.style.left), parseFloat(panel.style.top));
  };

  window.addEventListener("resize", onResize);

  handle.addEventListener("pointerdown", (event: PointerEvent) => {
    if (event.button !== 0) {
      return;
    }

    const rect = panel.getBoundingClientRect();
    pointerId = event.pointerId;
    offsetX = event.clientX - rect.left;
    offsetY = event.clientY - rect.top;
    placePanel(panel, rect.left, rect.top);
    handle.setPointerCapture(event.pointerId);
    event.preventDefault();
  });

  handle.addEventListener("pointermove", (event: PointerEvent) => {
    if (pointerId !== event.pointerId) {
      return;
    }

    placePanel(panel, event.clientX - offsetX, event.clientY - offsetY);
  });

  const endDrag = (event: PointerEvent) => {
    if (pointerId !== event.pointerId) {
      return;
    }

    pointerId = null;
    const rect = panel.getBoundingClientRect();
    try {
      localStorage.setItem(controlsPositionKey, JSON.stringify({ x: rect.left, y: rect.top }));
    } catch {
      // The panel can still be moved for this visit when storage is unavailable.
    }
  };

  handle.addEventListener("pointerup", endDrag);
  handle.addEventListener("pointercancel", endDrag);
}

function restoreControlsPosition(panel: HTMLElement) {
  let raw: string | null = null;
  try {
    raw = localStorage.getItem(controlsPositionKey);
  } catch {
    return;
  }

  if (!raw) {
    return;
  }

  try {
    const parsed = JSON.parse(raw) as { x?: number, y?: number };
    if (typeof parsed.x !== "number" || typeof parsed.y !== "number") {
      return;
    }

    placePanel(panel, parsed.x, parsed.y);
  } catch {
    try {
      localStorage.removeItem(controlsPositionKey);
    } catch {
      // Ignore storage failures.
    }
  }
}

function placePanel(panel: HTMLElement, x: number, y: number) {
  const nav = document.querySelector(".top-row");
  const minY = nav ? nav.getBoundingClientRect().bottom + 8 : 56;
  const maxX = Math.max(0, window.innerWidth - panel.offsetWidth);
  const maxY = Math.max(minY, window.innerHeight - panel.offsetHeight);
  panel.style.left = `${clamp(x, 0, maxX)}px`;
  panel.style.top = `${clamp(y, minY, maxY)}px`;
  panel.style.right = "auto";
}

function clamp(value: number, min: number, max: number) {
  return Math.min(Math.max(value, min), max);
}

export function getSize(): { width?: number, height?: number } {
  var e = document.getElementById("mandelbrot-tiles");
  if (!e) {
    return { width: undefined, height: undefined };
  }

  return { width: e.clientWidth, height: e.clientHeight };
}

const wheelSettleMs = 160;
const clickZoomMs = 180;
const clickZoomIn = 2;
const clickZoomOut = 0.5;
const dragThresholdSq = 16;

type ViewportApi = {
  invokeMethodAsync(method: string, ...args: unknown[]): Promise<void>;
};

type Gesture = {
  anchorX: number;
  anchorY: number;
  scale: number;
  translateX: number;
  translateY: number;
  animate: boolean;
};

type PointerSample = {
  x: number;
  y: number;
};

let viewport: HTMLElement | null = null;
let stage: HTMLElement | null = null;
let snapshot: HTMLCanvasElement | null = null;
let dotnet: ViewportApi | null = null;
let resizeObserver: ResizeObserver | null = null;
let paintListener: (() => void) | null = null;

let pointers = new Map<number, PointerSample>();
let gesture: Gesture | null = null;
let mode: "idle" | "wheeling" | "pinching" | "panning" | "clicking" = "idle";
let settling = false;
let pendingDx = 0;
let pendingDy = 0;
let panFrame = 0;
let panTask: Promise<void> = Promise.resolve();
let wheelTimer = 0;
let clickTimer = 0;
let suppressClick = false;
let drag: { id: number, x: number, y: number, moved: boolean } | null = null;
let pinchStart: { distance: number, midX: number, midY: number } | null = null;
let deferredScale = 1;
let deferredPoint: PointerSample | null = null;
let lastPointer: PointerSample | null = null;

function cancelRendering() {
  const cancel = (globalThis as typeof globalThis & { __fractalCancelRenders?: () => void }).__fractalCancelRenders;
  if (cancel) {
    cancel();
    return;
  }

  void dotnet?.invokeMethodAsync("CancelRendering");
}

function localPoint(clientX: number, clientY: number): PointerSample {
  const rect = viewport!.getBoundingClientRect();
  return { x: clientX - rect.left, y: clientY - rect.top };
}

function clampScale(scale: number) {
  return Math.min(80, Math.max(0.02, scale));
}

function applyStageTransform() {
  if (!stage) {
    return;
  }

  if (gesture) {
    stage.style.transition = gesture.animate ? `transform ${clickZoomMs}ms ease-out` : "none";
    stage.style.transformOrigin = `${gesture.anchorX}px ${gesture.anchorY}px`;
    stage.style.transform = `translate(${gesture.translateX + pendingDx}px, ${gesture.translateY + pendingDy}px) scale(${gesture.scale})`;
    return;
  }

  stage.style.transition = "none";
  stage.style.transformOrigin = "0 0";
  stage.style.transform = pendingDx || pendingDy ? `translate(${pendingDx}px, ${pendingDy}px)` : "none";
}

function notePan(dx: number, dy: number) {
  pendingDx += dx;
  pendingDy += dy;
  applyStageTransform();
  if (!panFrame) {
    panFrame = requestAnimationFrame(flushPan);
  }
}

function flushPan() {
  panFrame = 0;
  const dx = pendingDx;
  const dy = pendingDy;
  if (!dx && !dy) {
    return;
  }

  pendingDx = 0;
  pendingDy = 0;
  panTask = panTask.then(async () => {
    await dotnet?.invokeMethodAsync("ApplyPan", dx, dy);
    applyStageTransform();
  }).catch((error) => {
    console.error(error);
    applyStageTransform();
  });
}

async function drainPan() {
  if (panFrame) {
    cancelAnimationFrame(panFrame);
    panFrame = 0;
  }

  if (pendingDx || pendingDy) {
    flushPan();
  }

  await panTask;
}

function wheelFactor(event: WheelEvent) {
  let delta = event.deltaY;
  if (event.deltaMode === WheelEvent.DOM_DELTA_LINE) {
    delta *= 16;
  } else if (event.deltaMode === WheelEvent.DOM_DELTA_PAGE) {
    delta *= viewport?.clientHeight || window.innerHeight;
  }

  const clamped = Math.max(-120, Math.min(120, delta));
  return Math.exp(-clamped * 0.003);
}

function beginZoom(anchorX: number, anchorY: number, scale: number, animate: boolean) {
  if (!gesture) {
    cancelRendering();
  }

  gesture = {
    anchorX,
    anchorY,
    scale: clampScale(scale),
    translateX: 0,
    translateY: 0,
    animate,
  };
  applyStageTransform();
}

function updateWheel(anchorX: number, anchorY: number, factor: number) {
  if (mode !== "wheeling" || !gesture) {
    mode = "wheeling";
    beginZoom(anchorX, anchorY, factor, false);
  } else {
    gesture.scale = clampScale(gesture.scale * factor);
    gesture.animate = false;
    applyStageTransform();
  }

  window.clearTimeout(wheelTimer);
  wheelTimer = window.setTimeout(() => requestCommit(), wheelSettleMs);
}

function scheduleClickCommit() {
  const onEnd = (event: TransitionEvent) => {
    if (event.propertyName !== "transform") {
      return;
    }

    stage?.removeEventListener("transitionend", onEnd);
    window.clearTimeout(clickTimer);
    requestCommit();
  };

  stage?.addEventListener("transitionend", onEnd);
  window.clearTimeout(clickTimer);
  clickTimer = window.setTimeout(() => {
    stage?.removeEventListener("transitionend", onEnd);
    requestCommit();
  }, clickZoomMs + 40);
}

function readTranslate(element: HTMLElement) {
  const match = /translate\(\s*(-?[\d.]+)px\s*,\s*(-?[\d.]+)px\s*\)/.exec(element.style.transform || "");
  if (!match) {
    return { x: 0, y: 0 };
  }

  return { x: parseFloat(match[1]) || 0, y: parseFloat(match[2]) || 0 };
}

function renderFlattened(current: Gesture) {
  const width = viewport!.clientWidth;
  const height = viewport!.clientHeight;
  const buffer = document.createElement("canvas");
  buffer.width = Math.max(1, width);
  buffer.height = Math.max(1, height);
  const ctx = buffer.getContext("2d");
  if (!ctx) {
    return buffer;
  }

  ctx.fillStyle = "#000";
  ctx.fillRect(0, 0, buffer.width, buffer.height);
  ctx.imageSmoothingEnabled = true;
  ctx.imageSmoothingQuality = "high";
  const { scale, anchorX, anchorY, translateX, translateY } = current;
  ctx.setTransform(
    scale,
    0,
    0,
    scale,
    anchorX * (1 - scale) + translateX,
    anchorY * (1 - scale) + translateY,
  );

  if (snapshot && snapshot.width > 0 && snapshot.height > 0) {
    const offset = readTranslate(snapshot);
    ctx.drawImage(snapshot, offset.x, offset.y);
  }

  const slots = viewport!.querySelectorAll<HTMLElement>(".tile-slot");
  slots.forEach((slot) => {
    const canvas = slot.querySelector("canvas");
    if (!canvas || canvas.width === 0 || canvas.height === 0) {
      return;
    }

    const left = parseFloat(slot.style.left) || 0;
    const top = parseFloat(slot.style.top) || 0;
    ctx.drawImage(canvas, left, top, canvas.width, canvas.height);
  });

  return buffer;
}

function sizeSnapshot() {
  if (!snapshot || !viewport) {
    return;
  }

  const width = viewport.clientWidth;
  const height = viewport.clientHeight;
  if (width > 0 && height > 0 && (snapshot.width !== width || snapshot.height !== height)) {
    snapshot.width = width;
    snapshot.height = height;
  }
}

function blitSnapshot(buffer: HTMLCanvasElement) {
  if (!snapshot) {
    return;
  }

  sizeSnapshot();
  const ctx = snapshot.getContext("2d");
  if (!ctx) {
    return;
  }

  ctx.setTransform(1, 0, 0, 1, 0, 0);
  ctx.clearRect(0, 0, snapshot.width, snapshot.height);
  ctx.drawImage(buffer, 0, 0);
}

let commitChain: Promise<void> = Promise.resolve();

function requestCommit() {
  if (!gesture || settling) {
    return;
  }

  const current = { ...gesture };
  window.clearTimeout(wheelTimer);
  window.clearTimeout(clickTimer);
  if (Math.abs(current.scale - 1) < 1e-6 && Math.abs(current.translateX) < 0.5 && Math.abs(current.translateY) < 0.5) {
    gesture = null;
    mode = "idle";
    applyStageTransform();
    return;
  }

  mode = "idle";
  settling = true;
  commitChain = commitChain.then(async () => {
    try {
      await drainPan();
      const buffer = renderFlattened(current);
      gesture = null;
      viewport?.classList.add("is-settling");
      if (stage) {
        stage.style.transition = "none";
        stage.style.transform = "none";
      }

      blitSnapshot(buffer);
      if (snapshot) {
        snapshot.style.transform = "none";
      }
      await dotnet?.invokeMethodAsync(
        "CommitZoom",
        current.anchorX,
        current.anchorY,
        current.scale,
        current.translateX,
        current.translateY,
      );
    } catch (error) {
      console.error(error);
      gesture = null;
    } finally {
      viewport?.classList.remove("is-settling");
      settling = false;
      applyStageTransform();
      if (deferredPoint && Math.abs(deferredScale - 1) > 1e-6) {
        const point = deferredPoint;
        const scale = deferredScale;
        deferredPoint = null;
        deferredScale = 1;
        updateWheel(point.x, point.y, scale);
      }
    }
  });
}

function maybeClearSnapshot() {
  if (!viewport || !snapshot || viewport.classList.contains("is-settling") || gesture) {
    return;
  }

  const canvases = viewport.querySelectorAll<HTMLCanvasElement>("canvas.mandelbrot-tile");
  if (canvases.length === 0) {
    return;
  }

  for (const canvas of canvases) {
    if (canvas.dataset.painted !== "true") {
      return;
    }
  }

  const ctx = snapshot.getContext("2d");
  ctx?.clearRect(0, 0, snapshot.width, snapshot.height);
}

function pointerDistance() {
  const points = [...pointers.values()];
  if (points.length < 2) {
    return 0;
  }

  return Math.hypot(points[0].x - points[1].x, points[0].y - points[1].y);
}

function pointerMidpoint(): PointerSample {
  const points = [...pointers.values()];
  return {
    x: (points[0].x + points[1].x) / 2,
    y: (points[0].y + points[1].y) / 2,
  };
}

function beginPinch() {
  if (!viewport) {
    return;
  }

  cancelRendering();
  const mid = pointerMidpoint();
  const local = localPoint(mid.x, mid.y);
  pinchStart = { distance: Math.max(1, pointerDistance()), midX: local.x, midY: local.y };
  mode = "pinching";
  drag = null;
  viewport.classList.remove("is-panning");
  gesture = {
    anchorX: local.x,
    anchorY: local.y,
    scale: 1,
    translateX: 0,
    translateY: 0,
    animate: false,
  };
  applyStageTransform();
}

function updatePinch() {
  if (!pinchStart || !gesture || pointers.size < 2) {
    return;
  }

  const mid = localPoint(pointerMidpoint().x, pointerMidpoint().y);
  gesture.scale = clampScale(pointerDistance() / pinchStart.distance);
  gesture.translateX = mid.x - pinchStart.midX;
  gesture.translateY = mid.y - pinchStart.midY;
  gesture.animate = false;
  applyStageTransform();
}

function onPointerDown(event: PointerEvent) {
  if (!viewport || (event.button > 0 && event.pointerType === "mouse")) {
    return;
  }

  event.preventDefault();

  pointers.set(event.pointerId, { x: event.clientX, y: event.clientY });
  lastPointer = localPoint(event.clientX, event.clientY);
  viewport.setPointerCapture(event.pointerId);

  if (pointers.size === 2) {
    suppressClick = true;
    beginPinch();
    return;
  }

  if (settling || mode === "wheeling" || mode === "clicking") {
    return;
  }

  drag = { id: event.pointerId, x: event.clientX, y: event.clientY, moved: false };
}

function onPointerMove(event: PointerEvent) {
  if (!pointers.has(event.pointerId)) {
    if (viewport) {
      lastPointer = localPoint(event.clientX, event.clientY);
    }
    return;
  }

  pointers.set(event.pointerId, { x: event.clientX, y: event.clientY });
  lastPointer = localPoint(event.clientX, event.clientY);

  if (pointers.size >= 2 && mode === "pinching") {
    updatePinch();
    return;
  }

  if (!drag || drag.id !== event.pointerId || mode === "pinching" || settling) {
    return;
  }

  const dx = event.clientX - drag.x;
  const dy = event.clientY - drag.y;
  if (!drag.moved) {
    if (dx * dx + dy * dy < dragThresholdSq) {
      return;
    }

    drag.moved = true;
    mode = "panning";
    viewport?.classList.add("is-panning");
  }

  drag.x = event.clientX;
  drag.y = event.clientY;
  notePan(dx, dy);
}

function onPointerUp(event: PointerEvent) {
  const sample = pointers.get(event.pointerId);
  pointers.delete(event.pointerId);

  if (mode === "pinching") {
    if (pointers.size < 2) {
      pinchStart = null;
      mode = "idle";
      requestCommit();
      suppressClick = true;
    }
    return;
  }

  const activeDrag = drag && drag.id === event.pointerId ? drag : null;
  if (activeDrag) {
    drag = null;
    viewport?.classList.remove("is-panning");
    if (mode === "panning") {
      mode = "idle";
      if (panFrame) {
        cancelAnimationFrame(panFrame);
        panFrame = 0;
      }
      flushPan();
      return;
    }
  }

  if (suppressClick) {
    if (pointers.size === 0) {
      suppressClick = false;
    }
    return;
  }

  if (!sample || event.button !== 0 || settling || mode === "wheeling" || mode === "clicking") {
    return;
  }

  const local = localPoint(event.clientX, event.clientY);
  mode = "clicking";
  beginZoom(local.x, local.y, clickZoomIn, true);
  scheduleClickCommit();
}

function onPointerCancel(event: PointerEvent) {
  onPointerUp(event);
}

function onContextMenu(event: MouseEvent) {
  event.preventDefault();
  if (!viewport || settling || mode === "pinching" || mode === "panning" || mode === "wheeling" || mode === "clicking") {
    return;
  }

  const local = localPoint(event.clientX, event.clientY);
  mode = "clicking";
  beginZoom(local.x, local.y, clickZoomOut, true);
  scheduleClickCommit();
}

function onWheel(event: WheelEvent) {
  event.preventDefault();
  if (!viewport || event.deltaY === 0) {
    return;
  }

  const local = localPoint(event.clientX, event.clientY);
  lastPointer = local;
  const factor = wheelFactor(event);
  if (settling || mode === "clicking" || mode === "pinching") {
    deferredScale *= factor;
    deferredPoint = local;
    if (mode === "clicking") {
      requestCommit();
    }
    return;
  }

  updateWheel(local.x, local.y, factor);
}

function onTilePainted() {
  maybeClearSnapshot();
}

export function initViewport(viewportElement: HTMLElement, stageElement: HTMLElement, snapshotElement: HTMLCanvasElement, api: ViewportApi) {
  disposeViewport();
  viewport = viewportElement;
  stage = stageElement;
  snapshot = snapshotElement;
  dotnet = api;
  document.documentElement.classList.add("mandelbrot-view");
  sizeSnapshot();

  viewport.addEventListener("pointerdown", onPointerDown);
  viewport.addEventListener("pointermove", onPointerMove);
  viewport.addEventListener("pointerup", onPointerUp);
  viewport.addEventListener("pointercancel", onPointerCancel);
  viewport.addEventListener("contextmenu", onContextMenu);
  viewport.addEventListener("wheel", onWheel, { passive: false });
  paintListener = onTilePainted;
  document.addEventListener("mandelbrot-tile-painted", paintListener);

  resizeObserver = new ResizeObserver(() => {
    sizeSnapshot();
    void dotnet?.invokeMethodAsync("OnViewportResize", viewport?.clientWidth ?? 0, viewport?.clientHeight ?? 0);
  });
  resizeObserver.observe(viewport);
}

export function prepareForRelayout() {
  window.clearTimeout(wheelTimer);
  window.clearTimeout(clickTimer);
  gesture = null;
  mode = "idle";
  pinchStart = null;
  drag = null;
  pendingDx = 0;
  pendingDy = 0;
  deferredScale = 1;
  deferredPoint = null;
  settling = false;
  viewport?.classList.remove("is-settling", "is-panning");
  if (stage) {
    stage.style.transition = "none";
    stage.style.transform = "none";
  }

  if (snapshot) {
    const ctx = snapshot.getContext("2d");
    ctx?.clearRect(0, 0, snapshot.width, snapshot.height);
  }
}

export function disposeViewport() {
  if (viewport) {
    viewport.removeEventListener("pointerdown", onPointerDown);
    viewport.removeEventListener("pointermove", onPointerMove);
    viewport.removeEventListener("pointerup", onPointerUp);
    viewport.removeEventListener("pointercancel", onPointerCancel);
    viewport.removeEventListener("contextmenu", onContextMenu);
    viewport.removeEventListener("wheel", onWheel);
  }

  if (paintListener) {
    document.removeEventListener("mandelbrot-tile-painted", paintListener);
    paintListener = null;
  }

  resizeObserver?.disconnect();
  resizeObserver = null;
  document.documentElement.classList.remove("mandelbrot-view");
  window.clearTimeout(wheelTimer);
  window.clearTimeout(clickTimer);
  if (panFrame) {
    cancelAnimationFrame(panFrame);
    panFrame = 0;
  }

  viewport = null;
  stage = null;
  snapshot = null;
  dotnet = null;
  gesture = null;
  pointers = new Map();
}
