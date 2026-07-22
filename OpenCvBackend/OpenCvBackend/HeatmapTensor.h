#pragma once

#include <opencv2/core.hpp>

#include <sstream>
#include <string>
#include <vector>

namespace OpenCvBackendNative
{
    inline std::string DescribeTensorShape(const cv::Mat& tensor)
    {
        std::ostringstream description;
        description << '[';

        for (int dimension = 0; dimension < tensor.dims; ++dimension)
        {
            if (dimension > 0)
            {
                description << ", ";
            }

            description << tensor.size[dimension];
        }

        description << ']';
        return description.str();
    }

    inline bool TryCreateSingleChannelHeatmap(
        const std::vector<cv::Mat>& outputs,
        cv::Mat& heatmap,
        std::string& error)
    {
        heatmap.release();
        error.clear();

        if (outputs.size() != 1)
        {
            error = "Expected exactly one DNN output tensor, but received "
                + std::to_string(outputs.size()) + ".";
            return false;
        }

        const cv::Mat& output = outputs.front();

        if (output.empty())
        {
            error = "The DNN output tensor is empty.";
            return false;
        }

        if (output.dims != 4)
        {
            error = "Expected a 4D NCHW output tensor, but received "
                + std::to_string(output.dims) + " dimensions with shape "
                + DescribeTensorShape(output) + ".";
            return false;
        }

        const int batch = output.size[0];
        const int channels = output.size[1];
        const int height = output.size[2];
        const int width = output.size[3];

        if (batch != 1 || channels != 1)
        {
            error = "Expected DNN output shape [1, 1, H, W], but received "
                + DescribeTensorShape(output) + ".";
            return false;
        }

        if (height <= 0 || width <= 0)
        {
            error = "DNN output height and width must be positive; received "
                + DescribeTensorShape(output) + ".";
            return false;
        }

        if (output.type() != CV_32FC1)
        {
            error = "Expected a single-channel float32 DNN output tensor.";
            return false;
        }

        try
        {
            const cv::Mat contiguousOutput = output.isContinuous() ? output : output.clone();
            const int heatmapShape[2] = { height, width };

            // Clone so the returned 2D matrix owns its data independently of outputs.
            heatmap = contiguousOutput.reshape(1, 2, heatmapShape).clone();
        }
        catch (const cv::Exception& exception)
        {
            error = "Failed to convert the DNN output tensor to a heatmap: ";
            error += exception.what();
            heatmap.release();
            return false;
        }

        return true;
    }
}
