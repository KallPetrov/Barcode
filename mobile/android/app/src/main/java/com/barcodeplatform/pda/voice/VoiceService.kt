package com.barcodeplatform.pda.voice

import android.content.Context
import android.speech.tts.TextToSpeech
import android.util.Log
import java.util.*

class VoiceService(context: Context) : TextToSpeech.OnInitListener {
    private var tts: TextToSpeech? = null
    private var isInitialized = false
    private val TAG = "VoiceService"

    init {
        tts = TextToSpeech(context, this)
    }

    override fun onInit(status: Int) {
        if (status == TextToSpeech.SUCCESS) {
            val result = tts?.setLanguage(Locale("bg", "BG"))
            if (result == TextToSpeech.LANG_MISSING_DATA || result == TextToSpeech.LANG_NOT_SUPPORTED) {
                Log.e(TAG, "Bulgarian language is not supported. Falling back to English.")
                tts?.setLanguage(Locale.US)
            }
            isInitialized = true
        } else {
            Log.e(TAG, "Initialization failed")
        }
    }

    fun speak(text: String) {
        val prefs = context.getSharedPreferences("voice_prefs", Context.MODE_PRIVATE)
        val enabled = prefs.getBoolean("voice_enabled", true)
        if (isInitialized && enabled) {
            tts?.speak(text, TextToSpeech.QUEUE_FLUSH, null, null)
        }
    }

    fun speakQueue(text: String) {
        if (isInitialized) {
            tts?.speak(text, TextToSpeech.QUEUE_ADD, null, null)
        }
    }

    fun shutdown() {
        tts?.stop()
        tts?.shutdown()
    }
}
