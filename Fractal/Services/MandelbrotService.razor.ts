class WorkerReadyEvent extends Event {
  constructor(public index: number) {
    super("workerReady");
  }
}

const onWorkerReady = (index: number) => {
  document.dispatchEvent(new WorkerReadyEvent(index));
};

interface WorkerState {
  ready: boolean;
}

function createWorkerState(worker: Worker, index: number) {
  let _privateReady = false;
  const workerState = {
    worker,
    get ready() {
      return _privateReady;
    },
    set ready(value: boolean) {
      if (value) {
        _privateReady = true;
        onWorkerReady(index);
      }
    }
  }

  return workerState;
}

const state = {
  workers: [] as WorkerState[],
  allWorkersReady: () => new Promise<boolean>((resolve, reject) => {
    if (state.workers.every(w => w.ready)) {
      resolve(true);
    } else {
      const promises = [] as Promise<boolean>[];
      for (let i = 0; i < state.workers.length; i++) {
        if (!state.workers[i].ready) {
          promises.push(new Promise((resolve, reject) => {
            document.addEventListener("workerReady", (event: Event) => {
              const wre = event as WorkerReadyEvent;
              if (wre && wre.index === i) {
                resolve(true);
              }
            })
          }));
        }
      }
      Promise.all(promises).then(() => resolve(true));
    }
  }),
};

/* ========================================================================= */
/*  Fetching configuration values, e.g. tile size                            */
/* ------------------------------------------------------------------------- */

// @ts-ignore
import { dotnet } from "/_framework/dotnet.js";

let assemblyExports: any;
let startupError: any;

try {
  const { getAssemblyExports, getConfig } = await dotnet.create();
  const config = getConfig();
  assemblyExports = await getAssemblyExports(config.mainAssemblyName);
} catch (err) {
  startupError = err;
}

const configuration = {
  _tileSize: undefined as number | undefined,
  get tileSize(): number {
    if (!this._tileSize) {
      this._tileSize = assemblyExports.Fractal.Services.SettingsService.GetTileSize();
    }
    return this._tileSize || 100;
  },

  _iterationLimit: undefined as number | undefined,
  get iterationLimit(): number {
    if (!this._iterationLimit) {
      this._iterationLimit = assemblyExports.Fractal.Services.SettingsService.GetIterationLimit();
      console.log("Iteration count fetched from .NET:", this._iterationLimit);
    }
    return this._iterationLimit || 250;
  }
}


/* ------------------------------------------------------------------------- */

const pendingRequests: any = {};
let pendingRequestId = 0;

const workerCount = navigator.hardwareConcurrency || 4;
let workers: Worker[] = [];

function sendRequestToWorker(request: any): Promise<Int32Array> {
  pendingRequestId++;
  const promise = new Promise<Int32Array>((resolve, reject) => {
    pendingRequests[pendingRequestId] = { resolve, reject };
  });

  const workerIndex = pendingRequestId % workers.length;
  const worker = workers[workerIndex];

  if (!state.workers[workerIndex].ready) {
    request.reject(new Error("Worker is not ready"));
    delete pendingRequests[pendingRequestId];
  } else {
    worker.postMessage({ ...request, requestId: pendingRequestId });
  }
  return promise;
}

const createMessageHandler = (i) => (e: MessageEvent) => {
  switch (e.data.status) {
    case "ready":
      if (state.workers[i] !== undefined) {
        state.workers[i].ready = true;
      }
      break;

    case "success":
      const request = pendingRequests[e.data.requestId];
      delete pendingRequests[e.data.requestId];
      if (e.data.error) {
        request.reject(new Error(e.data.error));
      } else {
        request.resolve(e.data.result);
      }
      break;

    default:
      console.log("Worker said:", e.data);
  }
}

/**
 * Creates a new worker for Mandelbrot calculations and sets up the message event listener.
 */
export async function initializeWorker() {
  // Create the worker
  for (let i = 0; i < workerCount; i++) {
    const worker = new Worker("./Workers/MandelbrotCalculation.razor.js", {
      type: "module",
    });
    worker.addEventListener("message", createMessageHandler(i));
    state.workers.push(createWorkerState(worker, i));
    workers.push(worker);
  }

  // Wait for the worker to be ready
  await state.allWorkersReady();
}

/**
 *
 * @param nr The number of steps along the real axis
 * @param ni Number of steps along the imaginary axis
 * @param rMin The minimum value along the real axis
 * @param rMax The maximum value along the real axis
 * @param iMin The minimum value along the imaginary axis
 * @param iMax The maximum value along the imaginary axis
 * @returns A promise that resolves to an array representing the Mandelbrot heights for the specified region
 */
export async function mandelbrot(
  elementId: string,
  requestId: number,
  nr: number,
  ni: number,
  rMin: number,
  rMax: number,
  iMin: number,
  iMax: number,
) {
  const workerIndex = requestId % workers.length;
  const worker = workers[workerIndex];

  if (!worker) {
    console.error("You must call initializeWorker() before calling mandelbrot()");
  }

  if (!state.workers[workerIndex].ready) {
    console.log("Worker is not ready, waiting...");
    await state.allWorkersReady();
  }

  const t = new Date().getTime();
  console.log(`[${requestId}] Sending Mandelbrot calculation request to worker`);

  const payload = { requestId, nr, ni, rMin, rMax, iMin, iMax };
  const response = await sendRequestToWorker({
    type: "mandelbrot",
    payload,
  });

  const elem = document.getElementById(elementId);
  if (!elem) {
    console.error(`Element with ID ${elementId} not found`);
    return;
  }

  const limit = configuration.iterationLimit;
  console.log(`[${requestId}] Iteration threshold: ${limit}`);

  var imageArray = new Uint8ClampedArray(response.length * 4);
  for (let i = 0; i < response.length; i++) {
    const h = response[i];
    // const t = (limit - 1.0 * h) / limit;
    const t = (1.0 * h) / limit;
    const [r, g, b, a] = h === limit ? [0, 0, 0, 255] : getColour(t);
    imageArray.set([r, g, b, a], i * 4);
  }

  var x = new Int32Array(response.buffer);
    (elem as HTMLCanvasElement).getContext("2d")?.putImageData(new ImageData(imageArray, nr, ni), 0, 0);

  const diff = new Date().getTime() - t;
  console.log(`[${requestId}] Received Mandelbrot calculation response from worker in ${diff} ms`, response);

  return response;
}

function getColour(t: number) {
  // const amplification = t < 0.9 ? 255 : 255 * Math.pow((1.0 - t) / 0.1, 2);
  const amplification = t > 0.1 ? 255 : 255 * Math.pow(t / 0.1, 2);

  const r = 0.5 + 0.5 * Math.cos(2 * Math.PI * (1.0 * t + 0.00))
  const g = 0.5 + 0.5 * Math.cos(2 * Math.PI * (1.0 * t + 0.33))
  const b = 0.5 + 0.5 * Math.cos(2 * Math.PI * (1.0 * t + 0.67))
  return [r * amplification, g * amplification, b * amplification, 255]
}


/**
 * Unwraps a JSObject into whatever C# expects it to be.
 *
 * This works around the fact that it's not currently possible to marshal a promise that resolves
 *  to an array directly from JavaScript to .NET. So, we return a promise which resolves to a
 *  JSObject, then use a synchronous function to unwrap that object into the desired type.
 *
 * @param jsObject A JSObject reference
 * @returns The exact same object, which C# will interpret as the desired type
 */
export function unwrapJsObject(jsObject: any) {
  return jsObject;
}
