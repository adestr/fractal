export function createWorker(
  numRe: number,
  numIm: number,
  minRe: number,
  maxRe: number,
  minIm: number,
  maxIm: number,
) {
  let myWorker: Worker | null = null;
  if (window.Worker) {
    myWorker = new Worker(
      new URL("/Scripts/mandelbrot-worker.js", import.meta.url),
      { type: "module" },
    );
  } else {
    console.error("Workers not supported by this browser");
    return;
  }

  myWorker.onmessage = (e) => {
    console.log("Message received from worker:", e.data);

    if (e.data.status === "ready") {
      myWorker?.postMessage({ numRe, numIm, minRe, maxRe, minIm, maxIm });
    } else if (e.data.status === "success") {
      console.log("Worker calculation successful:", e.data.data);
    } else if (e.data.status === "error") {
      console.error("Worker calculation failed:", e.data.error);
    }
  };
}
