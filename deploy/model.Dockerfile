FROM python:3.13-slim
WORKDIR /app
COPY models/model.json models/
COPY data/future.csv data/
COPY model_service/server.py model_service/
USER nobody
CMD ["python","-B","model_service/server.py"]
