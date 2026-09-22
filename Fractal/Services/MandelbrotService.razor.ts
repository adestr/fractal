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

function handleReady() {
  const p = new Promise((resolve, reject) => {
  });
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

  const diff = new Date().getTime() - t;
  console.log(`[${requestId}] Received Mandelbrot calculation response from worker in ${diff} ms`);

  return Array.from(response);
}

/**
 * Unwraps a JSObject into a byte[].
 *
 * This works around the fact that it's not currently possible to marshal a promise that resolves
 *  to an array directly from JavaScript to .NET. So, we return a promise which resolves to a
 *  JSObject, then use a synchronous function to unwrap that object into a byte[].
 *
 * @param jsObject A JSObject reference
 * @returns The exact same object, which C# will interpret as a byte[]
 */
export function unwrapJsObjectAsByteArray(jsObject: any) {
  return jsObject;
}
