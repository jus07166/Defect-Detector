# Defect Detector

## Pill dataset evaluation

The evaluator runs the real deep-learning backend against `Sample_Data\pill\test`, checks defect labels from the dataset folders and ground-truth mask presence, and writes classification metrics to CSV. It requires `OPENCV_DIR`, `bin\Release\model.onnx`, and Visual Studio 2022:

```powershell
powershell -ExecutionPolicy Bypass -File .\OpenCvBackend\OpenCvBackend.ImageTests\RunImageTests.ps1
```

Use `-MaxImagesPerCategory 1` for a quick smoke run. Optional `-MinimumRecall` and `-MinimumSpecificity` values between 0 and 1 can enforce a model-quality baseline.
