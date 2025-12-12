#include "includes/ACLDecompress.h"

// Forward declaration
template <bool bUseBindPose>
void ProcessTracks(const acl::compressed_tracks& tracks, FTransform* inRefPoses, size_t inRefPoseSize, FTrackToSkeletonMap* inTrackToSkeletonMap, size_t inTrackToSkeletonSize, FTransform* outAtom, size_t inAtomSize);

// ACL allocator
DLLEXPORT void* nAllocate(size_t size, size_t alignment) { return ACLAllocatorImpl.allocate(size, alignment); }
DLLEXPORT void nDeallocate(void* ptr, size_t size) { ACLAllocatorImpl.deallocate(ptr, size); }

// ACL compressed tracks
DLLEXPORT const char* nCompressedTracks_IsValid(acl::compressed_tracks* tracks, bool checkHash) { return tracks->is_valid(checkHash).c_str(); }
DLLEXPORT void nTracksHeader_SetDefaultScale(acl::acl_impl::tracks_header* header, uint32_t defaultScale) { header->set_default_scale(defaultScale); }

DLLEXPORT void nReadACLData(const acl::compressed_tracks& tracks, FTransform* inRefPoses, size_t inRefPoseSize, FTrackToSkeletonMap* inTrackToSkeletonMap, size_t inTrackToSkeletonSize, FTransform* outAtom, size_t inAtomSize)
{
    if (tracks.get_default_scale() != 0)
    {
        ProcessTracks<true>(tracks, inRefPoses, inRefPoseSize, inTrackToSkeletonMap, inTrackToSkeletonSize, outAtom, inAtomSize);
    }
    else
    {
        ProcessTracks<false>(tracks, inRefPoses, inRefPoseSize, inTrackToSkeletonMap, inTrackToSkeletonSize, outAtom, inAtomSize);
    }
}

DLLEXPORT void nReadCurveACLData(const acl::compressed_tracks& tracks, float* outFloatKeys, size_t outFloatKeysSize)
{
    uint32_t numSamples = tracks.get_num_samples_per_track();
    float sampleRate = tracks.get_sample_rate();
    float duration = tracks.get_finite_duration();

    DecompContextDefault context;
    context.initialize(tracks);

    FCUE4ParseCurveWriter writer(outFloatKeys, numSamples, numSamples);
    for (uint32_t sampleIndex = 0; sampleIndex < numSamples; ++sampleIndex)
    {
        const float sample_time = rtm::scalar_min(float(sampleIndex) / sampleRate, duration);
        context.seek(sample_time, acl::sample_rounding_policy::nearest);
        writer.SampleIndex = sampleIndex;
        context.decompress_tracks(writer);
    }
}

template <bool bUseBindPose>
void ProcessTracks(const acl::compressed_tracks& tracks, FTransform* inRefPoses, size_t inRefPoseSize, FTrackToSkeletonMap* inTrackToSkeletonMap, size_t inTrackToSkeletonSize, FTransform* outAtom, size_t inAtomSize)
{
    uint32_t numSamples = tracks.get_num_samples_per_track();
    float sampleRate = tracks.get_sample_rate();
    float duration = tracks.get_finite_duration();

    DecompContextDefault context;
    context.initialize(tracks);

    FCUE4ParseOutputWriter<bUseBindPose> writer(inRefPoses, inRefPoseSize, inTrackToSkeletonMap, inTrackToSkeletonSize, outAtom, inAtomSize, numSamples);
    for (uint32_t sampleIndex = 0; sampleIndex < numSamples; ++sampleIndex)
    {
        const float sample_time = rtm::scalar_min(float(sampleIndex) / sampleRate, duration);
        context.seek(sample_time, acl::sample_rounding_policy::nearest);
        writer.SampleIndex = sampleIndex;
        context.decompress_tracks(writer);
    }
}
