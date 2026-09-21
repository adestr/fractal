const WorkerReadyEvent = new Event("workerReady");
const onWorkerReady = () => {
  document.dispatchEvent(WorkerReadyEvent);
};
const state = {
  _workerReady: false,
  get workerReady() {
    return !!state._workerReady;
  },
  set workerReady(value: boolean) {
    state._workerReady = value;
    onWorkerReady();
  },
  whenReady: () =>
    new Promise((resolve, reject) => {
      if (state._workerReady) {
        resolve(true);
      } else {
        document.addEventListener("workerReady", () => resolve(true), {
          once: true,
        });
      }
    }),
};

const pendingRequests: any = {};
let pendingRequestId = 0;

const worker = new Worker("./Workers/MandelbrotCalculation.razor.js", {
  type: "module",
});

worker.addEventListener("message", (e) => {
  switch (e.data.status) {
    case "ready":
      state.workerReady = true;
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
});

function sendRequestToWorker(request: any): Promise<Int32Array> {
  pendingRequestId++;
  const promise = new Promise<Int32Array>((resolve, reject) => {
    pendingRequests[pendingRequestId] = { resolve, reject };
  });

  if (!state.workerReady) {
    request.reject(new Error("Worker is not ready"));
    delete pendingRequests[pendingRequestId];
  } else {
    worker.postMessage({ ...request, requestId: pendingRequestId });
  }
  return promise;
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
  nr: number,
  ni: number,
  rMin: number,
  rMax: number,
  iMin: number,
  iMax: number,
) {
  if (!state.workerReady) {
    console.log("Worker is not ready, waiting...");
    await state.whenReady();
  }

  const payload = { nr, ni, rMin, rMax, iMin, iMax };
  const response = await sendRequestToWorker({
    type: "mandelbrot",
    payload,
  });

  return Array.from(response);
}

/**
 * Unwraps a JSObject into an int[].
 *
 * This works around the fact that it's not currently possible to marshal a promise that resolves
 *  to an array directly from JavaScript to .NET. So, we return a promise which resolves to a
 *  JSObject, then use a synchronous function to unwrap that object into an int[].
 *
 * @param jsObject A JSObject reference
 * @returns int[]
 */
export function unwrapJsObjectAsIntArray(jsObject: any) {
  return jsObject;
}
