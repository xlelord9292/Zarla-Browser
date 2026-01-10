namespace Zarla.Core.Features;

/// <summary>
/// Provides Picture-in-Picture functionality for videos
/// </summary>
public static class PictureInPicture
{
    /// <summary>
    /// Gets the JavaScript to request Picture-in-Picture mode for the current video
    /// </summary>
    public static string GetPiPScript()
    {
        return @"
(function() {
    'use strict';

    // Find video element - check for YouTube, Twitch, or generic video
    let video = null;

    // YouTube
    if (window.location.hostname.includes('youtube.com')) {
        video = document.querySelector('video.html5-main-video');
    }
    // Twitch
    else if (window.location.hostname.includes('twitch.tv')) {
        video = document.querySelector('video');
    }
    // Netflix
    else if (window.location.hostname.includes('netflix.com')) {
        video = document.querySelector('video');
    }
    // Generic - find largest video
    else {
        const videos = document.querySelectorAll('video');
        let maxSize = 0;
        videos.forEach(v => {
            const size = v.videoWidth * v.videoHeight;
            if (size > maxSize) {
                maxSize = size;
                video = v;
            }
        });
    }

    if (!video) {
        alert('No video found on this page');
        return;
    }

    // Check if already in PiP
    if (document.pictureInPictureElement) {
        document.exitPictureInPicture();
        return;
    }

    // Request PiP
    if (document.pictureInPictureEnabled) {
        video.requestPictureInPicture()
            .catch(err => {
                console.error('PiP error:', err);
                alert('Picture-in-Picture not supported for this video');
            });
    } else {
        alert('Picture-in-Picture is not supported in this browser');
    }
})();
";
    }
}
