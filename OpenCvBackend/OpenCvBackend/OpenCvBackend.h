#pragma once

namespace OpenCvBackend {
	public ref class BackendProcessor
	{
	public:
		BackendProcessor();
		~BackendProcessor();

		// 이미지 경로를 받아 윤곽선을 추출한 후, BMP 포맷의 바이트 배열로 반환합니다.
		cli::array<System::Byte>^ GetContourImage(
			System::String^ imagePath,
			[System::Runtime::InteropServices::Out] System::Collections::Generic::List<cli::array<System::Byte>^>^% outDefectList,
			int minDefectPixels,
			int thresholdSensitivity,
			double minShapeArea,
			bool isDeepLearningMode,
			double anomalyThreshold,
			double maxExpectedScore 
		);
	};
}
