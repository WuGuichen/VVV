function analyzeAudioBuffer(audioBuffer, detailed = false) {
  const peakCount = 480;
  const peaks = new Float32Array(peakCount);
  const channels = Array.from({ length: audioBuffer.numberOfChannels }, (_, index) => audioBuffer.getChannelData(index));
  const samplesPerPeak = Math.max(1, Math.ceil(audioBuffer.length / peakCount));
  const analysisStride = Math.max(1, Math.ceil(audioBuffer.length / 320000));
  const activeThreshold = Math.pow(10, -48 / 20);
  const blockLength = Math.max(1, Math.round(audioBuffer.sampleRate * .05));
  const blockSquares = [];
  let absolutePeak = 0;
  let peakSample = 0;
  let sum = 0;
  let squareSum = 0;
  let clippedSamples = 0;
  let sampledCount = 0;
  let firstActiveSample = -1;
  let lastActiveSample = -1;
  let blockSquareSum = 0;
  let blockSampleCount = 0;

  for (let bin = 0; bin < peakCount; bin++) {
    const start = bin * samplesPerPeak;
    const end = Math.min(audioBuffer.length, start + samplesPerPeak);
    let peak = 0;
    for (let channelIndex = 0; channelIndex < channels.length; channelIndex++) {
      const channel = channels[channelIndex];
      for (let sampleIndex = start; sampleIndex < end; sampleIndex++) peak = Math.max(peak, Math.abs(channel[sampleIndex]));
    }
    peaks[bin] = peak;
  }

  for (let sampleIndex = 0; sampleIndex < audioBuffer.length; sampleIndex += analysisStride) {
    let sample = 0;
    for (let channelIndex = 0; channelIndex < channels.length; channelIndex++) sample += channels[channelIndex][sampleIndex] || 0;
    sample /= Math.max(1, channels.length);
    const magnitude = Math.abs(sample);
    if (magnitude > absolutePeak) { absolutePeak = magnitude; peakSample = sampleIndex; }
    if (magnitude >= .999) clippedSamples++;
    if (magnitude >= activeThreshold) {
      if (firstActiveSample < 0) firstActiveSample = sampleIndex;
      lastActiveSample = sampleIndex;
    }
    sum += sample;
    squareSum += sample * sample;
    blockSquareSum += sample * sample;
    blockSampleCount++;
    sampledCount++;
    if (sampleIndex % blockLength < analysisStride || sampleIndex + analysisStride >= audioBuffer.length) {
      blockSquares.push(blockSquareSum / Math.max(1, blockSampleCount));
      blockSquareSum = 0;
      blockSampleCount = 0;
    }
  }

  const meanSquare = squareSum / Math.max(1, sampledCount);
  const rms = Math.sqrt(meanSquare);
  const peakDb = amplitudeDb(absolutePeak);
  const rmsDb = amplitudeDb(rms);
  const blockDb = blockSquares.map(value => 10 * Math.log10(Math.max(value, Number.EPSILON))).filter(Number.isFinite).sort((a,b) => a - b);
  const lowBlock = percentile(blockDb, .15);
  const highBlock = percentile(blockDb, .9);
  const duration = audioBuffer.duration;
  const peakTime = peakSample / audioBuffer.sampleRate;
  const firstActiveTime = firstActiveSample < 0 ? duration : firstActiveSample / audioBuffer.sampleRate;
  const lastActiveTime = lastActiveSample < 0 ? 0 : lastActiveSample / audioBuffer.sampleRate;
  const dcOffset = sum / Math.max(1, sampledCount);
  const bands = analyzeFrequencyBands(channels, audioBuffer.sampleRate, audioBuffer.length);
  const issues = [];
  if (clippedSamples) issues.push({ tone:"error", code:"CLIPPING", label:`检测到 ${clippedSamples} 个削波采样`, time:peakTime });
  if (Math.abs(dcOffset) > .005) issues.push({ tone:"warn", code:"DC_OFFSET", label:`DC Offset ${(dcOffset * 100).toFixed(2)}%`, time:0 });
  if (firstActiveTime > .035) issues.push({ tone:"warn", code:"HEAD_SILENCE", label:`头部静音 ${formatDuration(firstActiveTime)}`, time:firstActiveTime });
  if (duration - lastActiveTime > .12) issues.push({ tone:"info", code:"TAIL_SILENCE", label:`尾部静音 ${formatDuration(duration - lastActiveTime)}`, time:lastActiveTime });
  if (lastActiveTime - peakTime < .12) issues.push({ tone:"warn", code:"SHORT_DECAY", label:`尾音可能过短 · ${formatDuration(Math.max(0, lastActiveTime - peakTime))}`, time:lastActiveTime });

  return {
    peaks,
    duration,
    channels: audioBuffer.numberOfChannels,
    sampleRate: audioBuffer.sampleRate,
    peakDb,
    rmsDb,
    lufsEstimate: -0.691 + 10 * Math.log10(Math.max(meanSquare, Number.EPSILON)),
    crestDb: peakDb - rmsDb,
    dynamicRangeDb: Math.max(0, highBlock - lowBlock),
    dcOffset,
    clippedSamples,
    firstActiveTime,
    lastActiveTime,
    peakTime,
    decaySeconds: Math.max(0, lastActiveTime - peakTime),
    bands,
    issues,
    spectrogram: detailed ? buildSpectrogram(channels, audioBuffer.sampleRate, audioBuffer.length) : null
  };
}

function analyzeFrequencyBands(channels, sampleRate, length) {
  const stride = Math.max(1, Math.ceil(length / 240000));
  const effectiveRate = sampleRate / stride;
  const lowAlpha = 1 - Math.exp(-2 * Math.PI * 250 / effectiveRate);
  const midAlpha = 1 - Math.exp(-2 * Math.PI * 4000 / effectiveRate);
  let lowPass = 0, midPass = 0, lowEnergy = 0, midEnergy = 0, highEnergy = 0, sampleCount = 0;
  for (let sampleIndex = 0; sampleIndex < length; sampleIndex += stride) {
    let sample = 0;
    for (let channelIndex = 0; channelIndex < channels.length; channelIndex++) sample += channels[channelIndex][sampleIndex] || 0;
    sample /= Math.max(1, channels.length);
    lowPass += lowAlpha * (sample - lowPass);
    midPass += midAlpha * (sample - midPass);
    const mid = midPass - lowPass;
    const high = sample - midPass;
    lowEnergy += lowPass * lowPass;
    midEnergy += mid * mid;
    highEnergy += high * high;
    sampleCount++;
  }
  const values = [lowEnergy, midEnergy, highEnergy].map(value => Math.sqrt(value / Math.max(1, sampleCount)));
  const max = Math.max(...values, Number.EPSILON);
  return { low: Math.round(Math.sqrt(values[0] / max) * 100), mid: Math.round(Math.sqrt(values[1] / max) * 100), high: Math.round(Math.sqrt(values[2] / max) * 100) };
}

function buildSpectrogram(channels, sampleRate, length) {
  const columns = 96;
  const rows = 48;
  const windowSize = 256;
  const values = new Float32Array(columns * rows);
  const nyquist = sampleRate / 2;
  let maxPower = Number.EPSILON;
  for (let column = 0; column < columns; column++) {
    const center = Math.floor(column / Math.max(1, columns - 1) * Math.max(0, length - 1));
    for (let row = 0; row < rows; row++) {
      const frequency = 20 * Math.pow(Math.max(1, nyquist / 20), row / Math.max(1, rows - 1));
      const omega = 2 * Math.PI * frequency / sampleRate;
      let real = 0, imaginary = 0;
      for (let offset = 0; offset < windowSize; offset++) {
        const sampleIndex = Math.max(0, Math.min(length - 1, center + offset - windowSize / 2));
        let sample = 0;
        for (let channelIndex = 0; channelIndex < channels.length; channelIndex++) sample += channels[channelIndex][sampleIndex] || 0;
        sample /= Math.max(1, channels.length);
        const window = .5 - .5 * Math.cos(2 * Math.PI * offset / Math.max(1, windowSize - 1));
        real += sample * window * Math.cos(omega * offset);
        imaginary -= sample * window * Math.sin(omega * offset);
      }
      const power = real * real + imaginary * imaginary;
      values[column * rows + row] = power;
      maxPower = Math.max(maxPower, power);
    }
  }
  for (let index = 0; index < values.length; index++) values[index] = Math.max(0, Math.min(1, (10 * Math.log10(values[index] / maxPower) + 72) / 72));
  return { columns, rows, values };
}

function drawSpectrogram(canvas, spectrogram) {
  if (!canvas || !spectrogram) return;
  const context = canvas.getContext("2d");
  const image = context.createImageData(spectrogram.columns, spectrogram.rows);
  for (let column = 0; column < spectrogram.columns; column++) {
    for (let row = 0; row < spectrogram.rows; row++) {
      const value = spectrogram.values[column * spectrogram.rows + row];
      const color = spectrogramColor(value);
      const pixel = ((spectrogram.rows - row - 1) * spectrogram.columns + column) * 4;
      image.data[pixel] = color[0]; image.data[pixel + 1] = color[1]; image.data[pixel + 2] = color[2]; image.data[pixel + 3] = 255;
    }
  }
  const buffer = document.createElement("canvas");
  buffer.width = spectrogram.columns; buffer.height = spectrogram.rows;
  buffer.getContext("2d").putImageData(image, 0, 0);
  context.imageSmoothingEnabled = true;
  context.drawImage(buffer, 0, 0, canvas.width, canvas.height);
}

function spectrogramColor(value) {
  const stops = [[7,10,24],[39,23,88],[105,36,124],[203,61,91],[245,125,48],[250,213,119]];
  const scaled = Math.max(0, Math.min(stops.length - 1.001, value * (stops.length - 1)));
  const index = Math.floor(scaled), mix = scaled - index, a = stops[index], b = stops[index + 1];
  return a.map((channel, channelIndex) => Math.round(channel + (b[channelIndex] - channel) * mix));
}

function amplitudeDb(value) { return 20 * Math.log10(Math.max(Math.abs(value), Number.EPSILON)); }
function percentile(values, ratio) { if (!values.length) return -120; return values[Math.max(0, Math.min(values.length - 1, Math.floor((values.length - 1) * ratio)))]; }
