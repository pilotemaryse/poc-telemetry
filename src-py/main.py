import asyncio
import logging
from fastapi import FastAPI, HTTPException

logging.basicConfig(level=logging.INFO)
log = logging.getLogger("demo-python")

app = FastAPI(title="demo-python")


@app.get("/")
def root():
    return {"service": "demo-python", "endpoints": ["/errors/throw", "/errors/notfound", "/errors/badrequest", "/errors/slow"]}


@app.get("/errors/throw")
def throw():
    log.error("Triggered manual exception for trace testing")
    raise RuntimeError("Simulated error from demo-python")


@app.get("/errors/notfound")
def notfound():
    log.warning("Returning 404 for trace testing")
    raise HTTPException(status_code=404, detail="Simulated 404")


@app.get("/errors/badrequest")
def badrequest():
    log.warning("Returning 400 for trace testing")
    raise HTTPException(status_code=400, detail="Simulated bad request")


@app.get("/errors/slow")
async def slow():
    log.info("Starting slow request")
    await asyncio.sleep(3)
    log.info("Slow request done")
    return {"message": "Took 3s"}
