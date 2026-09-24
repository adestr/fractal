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

interface MandelbrotPayload {
  requestId: number;
  nr: number;
  ni: number;
  rMin: number;
  rMax: number;
  iMin: number;
  iMax: number;
  maxIterations: number;
}

interface MandelbrotJob {
  type: "mandelbrot";
  requestId: number;
  generation: number;
  payload: MandelbrotPayload;
}

interface CancelJob {
  type: "cancel";
  generation: number;
}

const rowsPerChunk = 32;
let generation = 0;
const queue: MandelbrotJob[] = [];
let pumping = false;

function yieldToEvents() {
  return new Promise<void>((resolve) => {
    const channel = new MessageChannel();
    channel.port1.onmessage = () => {
      channel.port1.close();
      resolve();
    };
    channel.port2.postMessage(undefined);
  });
}

function postCancelled(requestId: number) {
  self.postMessage({ status: "cancelled", requestId });
}

self.addEventListener("message", (event: MessageEvent<MandelbrotJob | CancelJob>) => {
  if (event.data.type === "cancel") {
    generation = event.data.generation;
    return;
  }

  if (event.data.type !== "mandelbrot") {
    return;
  }

  queue.push(event.data);
  if (!pumping) {
    pumping = true;
    void pump();
  }
});

async function pump() {
  try {
    while (queue.length > 0) {
      const job = queue.shift();
      if (!job) {
        continue;
      }

      if (job.generation !== generation) {
        postCancelled(job.requestId);
        continue;
      }

      await runJob(job);
    }
  } finally {
    pumping = false;
    if (queue.length > 0) {
      pumping = true;
      void pump();
    }
  }
}

async function runJob(job: MandelbrotJob) {
  const requestId = job.requestId;
  try {
    if (!assemblyExports) {
      throw new Error(startupError || "Worker exports could not be loaded");
    }

    await yieldToEvents();
    if (job.generation !== generation) {
      postCancelled(requestId);
      return;
    }

    const p = job.payload;
    const started = new Date().getTime();
    console.log(`[${p.requestId}] Sending Mandelbrot calculation request to C# layer`);
    const full = new Int32Array(p.nr * p.ni);

    for (let row = 0; row < p.ni; row += rowsPerChunk) {
      if (job.generation !== generation) {
        postCancelled(requestId);
        return;
      }

      const rowCount = Math.min(rowsPerChunk, p.ni - row);
      const strip = assemblyExports.Fractal.Workers.MandelbrotCalculation.CalculateStrip(
        p.requestId,
        p.nr,
        p.ni,
        row,
        rowCount,
        p.rMin,
        p.rMax,
        p.iMin,
        p.iMax,
        p.maxIterations,
      );
      const copy = new Int32Array(strip.length);
      strip.copyTo(copy);
      full.set(copy, row * p.nr);

      if (row + rowCount < p.ni) {
        await yieldToEvents();
      }
    }

    if (job.generation !== generation) {
      postCancelled(requestId);
      return;
    }

    const diff = new Date().getTime() - started;
    console.log(`[${p.requestId}] Received Mandelbrot calculation response from C# layer in ${diff} ms`);
    self.postMessage({
      status: "success",
      requestId,
      result: full,
    });
  } catch (err) {
    self.postMessage({
      status: "error",
      requestId,
      error: (err as Error)?.message || "Unknown error",
    });
    console.error(err);
  }
}

self.postMessage({
  status: "ready",
});
