#include "pch.h"
#include "OpenCvBackend.h"

#include <opencv2/core.hpp>       // 핵심 자료구조 (Mat, Point, Rect 등)
#include <opencv2/imgproc.hpp>    // 이미지 처리 (cvtColor, threshold, findContours 등)
#include <opencv2/imgcodecs.hpp>  // 이미지 입출력 (imread, imencode 등)
#include <opencv2/dnn.hpp>        // 딥러닝 모듈

//cuda 관련 헤더 추가
#include <opencv2/core/cuda.hpp>
#include <opencv2/cudaimgproc.hpp>
#include <opencv2/cudaarithm.hpp>
#include <opencv2/cudawarping.hpp>

#include <msclr/marshal_cppstd.h>
#include <vector>
#include <string>
#include <iostream>
#include <opencv2/dnn.hpp>

#include <windows.h>
#pragma comment(lib, "User32.lib")

using namespace System;
using namespace OpenCvBackend;
using namespace System::Collections::Generic;
using namespace System::Runtime::InteropServices;

// =====================================================================
//여기서부터는 C#의 간섭을 받지 않는 '순수 네이티브 C++' 영역입니다.
#pragma unmanaged 

#include "HeatmapTensor.h"

// 성능 최적화: 매번 모델을 로드하면 매우 느려 전역 변수로 한 번만 로드
static cv::dnn::Net* p_ai_net = nullptr;
static std::string cached_path;
static cv::Mat cached_src;
static cv::Mat cached_raw_heatmap;

void ResetNativeState()
{
    delete p_ai_net;
    p_ai_net = nullptr;
    cached_path.clear();
    cached_src.release();
    cached_raw_heatmap.release();
}

void ProcessImageNative(
    const std::string& path, 
    std::vector<uchar>& outMainBuffer, 
    std::vector<std::vector<uchar>>& outDefectBuffer, 
    int minDefectPixels,            // 이물 픽셀 정도
    int thresholdSensitivity,       // 이물 민감도
    double minShapeArea,            // 도형 인식 정도
	bool isDeepLearningMode,        // 딥러닝 모드 여부
    double anomalyThreshold,        // 이상치 감지 임계값
	double maxExpectedScore         // 최대 예상 점수
)
{
    // 이미지 불러오기
    cv::Mat src = cv::imread(path/*, cv::IMREAD_REDUCED_COLOR_8*/);
    if (src.empty()) return;

    if (path.empty()) return;

    //이물 검사 모드
    if (!isDeepLearningMode)
    {
        cv::Mat gray;
        cv::Mat dst = src.clone();

        // 1. 그레이스케일 변환
        cv::cvtColor(src, gray, cv::COLOR_BGR2GRAY);

        // 2. 전체 피사체(도형) 마스크 생성
        cv::Mat all_shapes_mask;
        cv::inRange(gray, cv::Scalar(30), cv::Scalar(230), all_shapes_mask);

        // 3. 찾은 피사체들의 외곽선 추출
        std::vector<std::vector<cv::Point>> shape_contours;
        cv::findContours(all_shapes_mask, shape_contours, cv::noArray(), cv::RETR_EXTERNAL, cv::CHAIN_APPROX_SIMPLE);

        bool isDefectFound = false;
        bool isDefectFoundTxt = false;

        // 4. 도형별 '평균값 기반' 적응형 검사 수행
        for (const auto& shape : shape_contours)
        {
            //면적 100 이하는 도형으로 취급하지 않고 패스
            if (cv::contourArea(shape) < minShapeArea) continue;

            //현재 검사 중인 도형 딱 1개만 하얗게 칠해진 개별 마스크 생성
            cv::Mat single_shape_mask = cv::Mat::zeros(gray.size(), CV_8UC1);
            cv::drawContours(single_shape_mask, std::vector<std::vector<cv::Point>>{shape}, -1, cv::Scalar(255), cv::FILLED);

            //테두리 그라데이션 노이즈를 피하기 위해 영역을 안쪽으로 1픽셀 깎아냄 (안전 ROI)
            cv::Mat inspection_roi;
            cv::Mat kernel = cv::getStructuringElement(cv::MORPH_RECT, cv::Size(5, 5));
            cv::erode(single_shape_mask, inspection_roi, kernel, cv::Point(-1, -1), 5);

            //현재 도형 내부(ROI)의 평균 밝기(Gray) 값 계산
            cv::Scalar mean_val = cv::mean(gray, inspection_roi);
            double avg_gray = mean_val[0];

            //원본 이미지와 평균값의 차이(절대값) 구하기
            cv::Mat diff;
            cv::absdiff(gray, cv::Scalar(avg_gray), diff);

            //평균값과 차이가 큰 픽셀만 이물질로 판정
            // 차이가 5(민감도)보다 크면 255(흰색, 이물질)로 만듦
            cv::Mat defect_mask;
            cv::threshold(diff, defect_mask, thresholdSensitivity, 255, cv::THRESH_BINARY);

            //도형 바깥 영역이 이물로 잡히지 않도록 안전 ROI 안의 이물만 남김
            cv::bitwise_and(defect_mask, inspection_roi, defect_mask);

            // 5. 이물질 윤곽선 찾기
            std::vector<std::vector<cv::Point>> defect_contours;
            cv::findContours(defect_mask, defect_contours, cv::noArray(), cv::RETR_EXTERNAL, cv::CHAIN_APPROX_SIMPLE);

            for (const auto& dp : defect_contours)
            {
                // 미세 이물 필터링 (구성 픽셀 3개 이상)
                if (dp.size() >= minDefectPixels)
                {
                    isDefectFound = true;

                    // 이물의 원래 위치와 크기 계산
                    cv::Rect boundingBox = cv::boundingRect(dp);

                    // 이물질 밖으로 띄울 여백(Margin) 2픽셀 추가
                    int margin = 2;
                    boundingBox.x -= margin;
                    boundingBox.y -= margin;
                    boundingBox.width += (margin * 2);
                    boundingBox.height += (margin * 2);

                    // 늘어난 박스가 전체 이미지 도화지 밖으로 튀어나가면 에러가 나므로 안전하게 자르기(Clamping)
                    boundingBox &= cv::Rect(0, 0, dst.cols, dst.rows);

                    //원본 이미지(src)에서 boundingBox 영역만큼만 이미지를 잘라냄
                    cv::Mat defect_crop = src(boundingBox).clone();

                    //잘라낸 조각 이미지를 bmp 포맷의 메모리 버퍼로 인코딩
                    std::vector<uchar> defect_buffer;
                    cv::imencode(".bmp", defect_crop, defect_buffer);

                    // 인코딩된 버퍼를 결과 리스트(outDefectBuffers)에 추가
                    outDefectBuffer.push_back(defect_buffer);

                    // 이물의 위치를 빨간색 사각형으로 표시 (두께 1)
                    cv::rectangle(dst, boundingBox, cv::Scalar(0, 0, 255), 1);
                }
            }

            // PASS 도형 테두리 초록색으로 표시
            if (!isDefectFound)
            {
                cv::drawContours(dst, std::vector<std::vector<cv::Point>>{shape}, -1, cv::Scalar(0, 255, 0), 1, cv::LINE_AA);
            }
            // FAIL 도형 테두리 빨간색으로 표시
            else
            {
                cv::drawContours(dst, std::vector<std::vector<cv::Point>>{shape}, -1, cv::Scalar(0, 0, 255), 1, cv::LINE_AA);
                isDefectFound = false;
                isDefectFoundTxt = true;
            }
        }

        // 6. 이물 존재 여부 텍스트 표시
        if (isDefectFoundTxt) {
            cv::putText(dst, "DEFECT DETECTED!", cv::Point(20, 50), cv::FONT_HERSHEY_SIMPLEX, 1.0, cv::Scalar(0, 0, 255), 2, cv::LINE_AA);
        }
        else {
            cv::putText(dst, "PASS", cv::Point(20, 50), cv::FONT_HERSHEY_SIMPLEX, 1.0, cv::Scalar(0, 255, 0), 2, cv::LINE_AA);
        }

        // 7. 결과를 버퍼에 저장 (참조로 전달받은 outMainBuffer 사용)
        cv::imencode(".bmp", dst, outMainBuffer);
    }
    // 알약 불량 검사 모드
    else
    {
        //파라미터 라이브 조정 최적화
        if (path != cached_path || cached_raw_heatmap.empty() || cached_src.empty())
        {
            // 1. 모델 로드 (최초 1회)
            if (p_ai_net == nullptr)
            {
                try 
                {
                    cv::dnn::Net temp_net = cv::dnn::readNetFromONNX("model.onnx");
                    
                    temp_net.setPreferableBackend(cv::dnn::DNN_BACKEND_CUDA);
                    temp_net.setPreferableTarget(cv::dnn::DNN_TARGET_CUDA);

                    p_ai_net = new cv::dnn::Net(temp_net);
                }
                catch (...)
                {
                    MessageBoxA(NULL, "Model Load Error", "에러", MB_OK);
                    return;
                }
            }

            //원본 사진 새로 로드하여 static 캐시에 저장
            cached_src = cv::imread(path, cv::IMREAD_COLOR);
            if (cached_src.empty()) return;

            // 2. Blob 생성
            cv::Mat blob = cv::dnn::blobFromImage(src, 1.0 / 255.0, cv::Size(256, 256), cv::Scalar(0, 0, 0), true, false);

            // 3. 추론
            p_ai_net->setInput(blob);
            std::vector<cv::Mat> outputs;

            // 모델 출력
            p_ai_net->forward(outputs);

            // 4. [1, 1, H, W] 형식을 검증하고 실제 출력 크기로 히트맵을 변환
            cv::Mat temp_heatmap;
            std::string tensorError;

            if (!OpenCvBackendNative::TryCreateSingleChannelHeatmap(outputs, temp_heatmap, tensorError))
            {
                MessageBoxA(NULL, tensorError.c_str(), "Invalid DNN Output Tensor", MB_OK | MB_ICONERROR);
                return;
            }

            // 5. 히트맵 원본 크기 확대 (실수형 데이터 상태 그대로 확대)
            cv::resize(temp_heatmap, cached_raw_heatmap, cached_src.size());

            cached_path = path;
        }
        cv::Mat overlay = cached_src.clone();

        // 6. 절대적인 수치(Threshold)로 불량 마스크 생성
        //이 점수를 넘어가면 불량(빨간 박스)으로 판정합니다.
        cv::Mat mask;
        cv::threshold(cached_raw_heatmap, mask, anomalyThreshold, 255, cv::THRESH_BINARY);
        mask.convertTo(mask, CV_8U); // 윤곽선 찾기를 위해 8비트로 변환

        //>> ROI 영역 자동 계산
        cv::Mat gray, pill_thresh;

        // 1. 흑백 전환 및 양극화
        cv::cvtColor(cached_src, gray, cv::COLOR_BGR2GRAY);
        cv::threshold(gray, pill_thresh, 30, 255, cv::THRESH_BINARY);

        // 2. 외각선 찾기 + 가장 큰 덩어리 선별
        std::vector<std::vector<cv::Point>> pill_contours;
        cv::findContours(pill_thresh, pill_contours, cv::RETR_EXTERNAL, cv::CHAIN_APPROX_SIMPLE);

        if (!pill_contours.empty()) 
        {
            // 여러 개의 덩어리 중 면적(Area)이 가장 큰 것(알약)의 위치를 찾습니다.
            auto largest_it = std::max_element(pill_contours.begin(), pill_contours.end(),
                [](const std::vector<cv::Point>& a, const std::vector<cv::Point>& b) {
                    return cv::contourArea(a) < cv::contourArea(b);
                });

            //3. 마스크 생성 및 결과 필터링
            cv::Mat auto_roi_mask = cv::Mat::zeros(cached_src.size(), CV_8UC1);
            cv::drawContours(auto_roi_mask, pill_contours, std::distance(pill_contours.begin(), largest_it), cv::Scalar(255), cv::FILLED);

            //Edge 노이즈 제거용 픽셀 깎이
            cv::Mat erode_element = cv::getStructuringElement(cv::MORPH_ELLIPSE, cv::Size(15, 15));
            cv::erode(auto_roi_mask, auto_roi_mask, erode_element);

            cv::bitwise_and(mask, auto_roi_mask, mask);
        }

        //<<

        // 7. 시각화를 위한 고정 스케일링 및 합성
        // 화면에 가장 진한 빨간색으로 표시될 최대 예상 점수입니다.
        cv::Mat visual_heatmap = cached_raw_heatmap * (255.0 / maxExpectedScore);
        visual_heatmap.convertTo(visual_heatmap, CV_8U);

        cv::Mat color_heatmap;
        cv::applyColorMap(visual_heatmap, color_heatmap, cv::COLORMAP_JET);

        cv::addWeighted(overlay, 0.7, color_heatmap, 0.1, 0, overlay);

        // 8. 불량 영역(마스크) 외곽선 찾고 박스 그리기
        std::vector<std::vector<cv::Point>> contours;
        cv::findContours(mask, contours, cv::RETR_EXTERNAL, cv::CHAIN_APPROX_SIMPLE);

        for (const auto& cnt : contours) {
            if (cv::contourArea(cnt) > 50) {
                cv::Rect box = cv::boundingRect(cnt);
                cv::rectangle(overlay, box, cv::Scalar(0, 0, 255), 2); // 빨간 박스

                cv::Mat crop = src(box & cv::Rect(0, 0, src.cols, src.rows)).clone();
                std::vector<uchar> buf;
                cv::imencode(".bmp", crop, buf);
                outDefectBuffer.push_back(buf);
            }
        }

        cv::imencode(".bmp", overlay, outMainBuffer);
    }
}
#pragma managed


BackendProcessor::BackendProcessor() {}
BackendProcessor::~BackendProcessor()
{
    ResetNativeState();
}

cli::array<System::Byte>^ BackendProcessor::GetContourImage(
    System::String^ imagePath,
    [System::Runtime::InteropServices::Out] System::Collections::Generic::List<cli::array<System::Byte>^>^% outDefectList,
    int minDefectPixels,
    int thresholdSensitivity,
    double minShapeArea,
    bool isDeepLearningMode,
    double anomalyThreshold,
    double maxExpectedScore 
)
{
    // 1. C# 문자열을 C++ 문자열로 변환 (여기서 System::String을 풀어줍니다)
    std::string nativePath = msclr::interop::marshal_as<std::string>(imagePath);

    std::vector<uchar> buffer;
    std::vector<std::vector<uchar>> DefectBuffer;

    // 2. 순수 네이티브 C++ 함수 호출
    ProcessImageNative(nativePath, buffer, DefectBuffer, minDefectPixels, thresholdSensitivity, minShapeArea, isDeepLearningMode, anomalyThreshold, maxExpectedScore);

    if (buffer.empty()) return nullptr;

    // 3. 메인 이미지를 C# 바이트 배열로 복사
    cli::array<System::Byte>^ byteArray = gcnew cli::array<System::Byte>(buffer.size());
    System::Runtime::InteropServices::Marshal::Copy(
        IntPtr((void*)buffer.data()), byteArray, 0, buffer.size()
    );

    // 4. out 파라미터 리스트 객체 생성 (여기도 명확하게 타입 명시)
    outDefectList = gcnew System::Collections::Generic::List<cli::array<System::Byte>^>();

    // 5. 각각의 이물질 버퍼 추가
    for (size_t i = 0; i < DefectBuffer.size(); i++)
    {
        cli::array<System::Byte>^ singleDefectArray = gcnew cli::array<System::Byte>(DefectBuffer[i].size());

        System::Runtime::InteropServices::Marshal::Copy(
            IntPtr((void*)DefectBuffer[i].data()), singleDefectArray, 0, DefectBuffer[i].size()
        );

        outDefectList->Add(singleDefectArray);
    }

    return byteArray;
}
